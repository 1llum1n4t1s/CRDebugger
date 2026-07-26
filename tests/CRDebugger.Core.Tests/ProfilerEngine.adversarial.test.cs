using CRDebugger.Core.Profiler;

namespace CRDebugger.Core.Tests;

/// <summary>
/// ProfilerEngine の嫌がらせテスト
/// </summary>
public sealed class ProfilerEngineAdversarialTests : IDisposable
{
    /// <summary>
    /// タイマー起点の状態が現れるのを待つ上限。
    /// <see cref="System.Threading.Timer"/> のコールバックはスレッドプール上で走るため、
    /// 少コア数の CI ランナーで他テストがプールを使っていると初回発火が数秒遅れることがある。
    /// 条件成立で即抜けるポーリングなので、長めに取っても正常時のテスト時間は増えない。
    /// </summary>
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(60);

    private readonly ProfilerEngine _engine;

    public ProfilerEngineAdversarialTests()
    {
        // 50msインターバルで高速テスト
        _engine = new ProfilerEngine(TimeSpan.FromMilliseconds(50));
    }

    public void Dispose() => _engine.Dispose();

    // ───────────────────────────────────
    // 🗡️ 境界値・極端入力 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 非常に短いインターバル（1ms）でもクラッシュしないこと
    /// </summary>
    [Fact]
    public void Constructor_VeryShortInterval_DoesNotCrash()
    {
        using var engine = new ProfilerEngine(TimeSpan.FromMilliseconds(1));
        engine.Start();
        Thread.Sleep(100); // しばらく走らせる
        // クラッシュしなければOK
    }

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// インターバル0でタイマーが暴走しないこと
    /// </summary>
    [Fact]
    public void Constructor_ZeroInterval_DoesNotHang()
    {
        using var engine = new ProfilerEngine(TimeSpan.Zero);
        engine.Start();

        // history が生成されるまで polling-with-deadline（CI ランナー向けに PollTimeout まで）
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (engine.GetHistory().Count == 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        var history = engine.GetHistory();
        // タイマーが動作すること（0ms = 即時かつ繰り返し）
        Assert.True(history.Count > 0);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// RecordFrameを大量に呼んでもオーバーフローしないこと
    /// </summary>
    [Fact]
    public void RecordFrame_MassiveCalls_DoesNotCrash()
    {
        _engine.Start();

        // 100万回フレーム記録
        for (int i = 0; i < 1_000_000; i++)
        {
            _engine.RecordFrame();
        }

        // Latest が生成されるまで polling-with-deadline（CI ランナー向けに PollTimeout まで）
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (_engine.Latest == null && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        var latest = _engine.Latest;
        // クラッシュせずスナップショットが取れること
        Assert.NotNull(latest);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// Start前のLatestがnullであること
    /// </summary>
    [Fact]
    public void Latest_BeforeStart_IsNull()
    {
        Assert.Null(_engine.Latest);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// Start前のGetHistoryが空リストを返すこと
    /// </summary>
    [Fact]
    public void GetHistory_BeforeStart_Empty()
    {
        Assert.Empty(_engine.GetHistory());
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// MaxHistorySize（120）を超えたら古いエントリが削除されること
    /// </summary>
    [Fact]
    public void History_ExceedsMaxSize_Trimmed()
    {
        using var engine = new ProfilerEngine(TimeSpan.FromMilliseconds(10));
        engine.Start();

        // 120件以上のスナップショットが取れるまで待つ
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (DateTime.UtcNow < deadline)
        {
            if (engine.GetHistory().Count >= ProfilerEngine.MaxHistorySize)
                break;
            Thread.Sleep(50);
        }

        Assert.True(engine.GetHistory().Count <= ProfilerEngine.MaxHistorySize);
    }

    // ───────────────────────────────────
    // ⚡ 並行性・レースコンディション (Concurrency Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category concurrency @severity critical
    /// 複数スレッドからRecordFrameを同時に呼んでもクラッシュしないこと
    /// </summary>
    [Fact]
    public async Task RecordFrame_ConcurrentCalls_NoCorruption()
    {
        _engine.Start();
        const int threadCount = 8;
        const int callsPerThread = 10_000;

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < callsPerThread; i++)
                _engine.RecordFrame();
        })).ToArray();

        await Task.WhenAll(tasks);

        // Latest が生成されるまで polling-with-deadline（CI ランナー向けに PollTimeout まで）
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (_engine.Latest == null && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        // クラッシュせず完了
        Assert.NotNull(_engine.Latest);
    }

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// SnapshotTakenイベントとGetHistory/Latestの同時アクセスがデッドロックしないこと
    /// </summary>
    [Fact]
    public void SnapshotTaken_ConcurrentHistoryAccess_NoDeadlock()
    {
        var snapshots = new List<ProfilerSnapshot>();
        _engine.SnapshotTaken += (_, s) =>
        {
            lock (snapshots) snapshots.Add(s);
            // イベントハンドラ内からGetHistoryを呼ぶ（デッドロックリスク）
            _ = _engine.GetHistory();
        };

        _engine.Start();

        // snapshots が取得されるまで polling-with-deadline（CI ランナー向けに PollTimeout まで）
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (true)
        {
            lock (snapshots)
            {
                if (snapshots.Count > 0) break;
            }
            if (DateTime.UtcNow >= deadline) break;
            Thread.Sleep(50);
        }

        lock (snapshots)
        {
            Assert.True(snapshots.Count > 0, "スナップショットが取得されるべき");
        }
    }

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// DisposeとOnTickの競合でクラッシュしないこと
    /// </summary>
    [Fact]
    public void Dispose_DuringOnTick_NoCrash()
    {
        using var engine = new ProfilerEngine(TimeSpan.FromMilliseconds(1));
        engine.Start();
        Thread.Sleep(10);
        // Disposeを即座に呼ぶ（OnTick実行中かもしれない）
        engine.Dispose();
        // 二重Dispose
        engine.Dispose();
    }

    // ───────────────────────────────────
    // 💀 リソース枯渇 (Resource Exhaustion)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category resource @severity medium
    /// ForceGarbageCollectionが繰り返し呼ばれてもハングしないこと
    /// </summary>
    [Fact]
    public void ForceGarbageCollection_RepeatedCalls_NoHang()
    {
        for (int i = 0; i < 10; i++)
        {
            _engine.ForceGarbageCollection();
        }
        // ハングしなければOK
    }

    /// <summary>
    /// @adversarial @category resource @severity high
    /// SnapshotTaken ハンドラが例外を投げても OnTick が握りつぶし、
    /// タイマースレッドの未処理例外でプロセスを落とさないこと。
    /// 例外が出た後もサンプリングが継続することまで確認する。
    /// <para>
    /// なお、マルチキャストデリゲートの仕様上、先行ハンドラが例外を投げると後続ハンドラは呼ばれない。
    /// これは .NET の挙動であり CRDebugger 側の不具合ではないため、ここでは検証対象にしない。
    /// </para>
    /// </summary>
    [Fact]
    public void SnapshotTaken_HandlerThrows_IsSwallowedAndSamplingContinues()
    {
        int throwingCalls = 0;

        // 必ず例外を投げるハンドラを登録する
        _engine.SnapshotTaken += (_, _) =>
        {
            Interlocked.Increment(ref throwingCalls);
            throw new InvalidOperationException("ハンドラ側の意図的な例外");
        };

        _engine.Start();

        // 2 回以上サンプリングされるまで polling-with-deadline（CI ランナー向けに PollTimeout まで）。
        // 2 回待つことで「1 回目の例外でタイマーが止まっていない」ことまで検証できる。
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (Interlocked.CompareExchange(ref throwingCalls, 0, 0) < 2 && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        // 例外を投げるハンドラが複数回呼ばれている＝OnTick が例外を握りつぶして継続している
        Assert.True(throwingCalls >= 2, $"サンプリングが継続していない（throwingCalls={throwingCalls}）");
        // 履歴も正常に積まれていること（ハンドラの例外がスナップショット記録を巻き込んでいない）
        Assert.NotEmpty(_engine.GetHistory());
    }

    // ───────────────────────────────────
    // 🔀 状態遷移の矛盾 (State Machine Abuse)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category state @severity high
    /// Startを複数回呼んでもクラッシュしないこと
    /// </summary>
    [Fact]
    public void Start_CalledMultipleTimes_NoCrash()
    {
        _engine.Start();
        _engine.Start(); // 二重Start
        Thread.Sleep(100);
        // メモリリークの可能性はあるが、クラッシュしないことを確認
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// Dispose後にRecordFrameを呼んでもクラッシュしないこと
    /// </summary>
    [Fact]
    public void RecordFrame_AfterDispose_NoCrash()
    {
        _engine.Start();
        _engine.Dispose();

        // Dispose後の操作
        _engine.RecordFrame();
        _ = _engine.Latest;
        _ = _engine.GetHistory();
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// Start前にRecordFrameを呼んでもクラッシュしないこと
    /// </summary>
    [Fact]
    public void RecordFrame_BeforeStart_NoCrash()
    {
        _engine.RecordFrame();
        _engine.RecordFrame();
        // クラッシュしなければOK
    }

    // ───────────────────────────────────
    // 🌪️ 環境異常 (Environmental Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// GcPauseTimeMs の契約確認。
    /// .NET 9+ では GC.GetTotalPauseDuration() の差分なので非負の実測値、
    /// .NET 8 では未サポートのため常に 0 になる。
    /// スイート全体を走らせると実際に GC ポーズが発生して 1ms 以上になりうるため、
    /// 「常に 0」ではなく TFM ごとの契約で検証する。
    /// </summary>
    [Fact]
    public void Snapshot_GcPauseTimeMs_MatchesRuntimeContract()
    {
        _engine.Start();

        // Snapshot history が生成されるまで polling-with-deadline（CI ランナー向けに PollTimeout まで）
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (_engine.GetHistory().Count == 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        var history = _engine.GetHistory();
        Assert.NotEmpty(history);
#if NET9_0_OR_GREATER
        // 累計ポーズ時間の差分なので負にはならない
        Assert.All(history, s => Assert.True(s.GcPauseTimeMs >= 0, $"GcPauseTimeMs が負値: {s.GcPauseTimeMs}"));
#else
        Assert.All(history, s => Assert.Equal(0, s.GcPauseTimeMs));
#endif
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// スナップショットのメモリ値が非負であること
    /// </summary>
    [Fact]
    public void Snapshot_MemoryValues_NonNegative()
    {
        _engine.Start();

        // Latest が生成されるまで polling-with-deadline（CI ランナー向けに PollTimeout まで）
        var deadline = DateTime.UtcNow.Add(PollTimeout);
        while (_engine.Latest == null && DateTime.UtcNow < deadline)
            Thread.Sleep(50);

        var latest = _engine.Latest;
        Assert.NotNull(latest);
        Assert.True(latest!.WorkingSetBytes >= 0);
        Assert.True(latest.GcTotalMemoryBytes >= 0);
        Assert.True(latest.Gen0Collections >= 0);
        Assert.True(latest.Gen1Collections >= 0);
        Assert.True(latest.Gen2Collections >= 0);
    }
}
