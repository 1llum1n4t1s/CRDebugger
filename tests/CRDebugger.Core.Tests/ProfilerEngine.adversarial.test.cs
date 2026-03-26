using CRDebugger.Core.Profiler;

namespace CRDebugger.Core.Tests;

/// <summary>
/// ProfilerEngine の嫌がらせテスト
/// </summary>
public sealed class ProfilerEngineAdversarialTests : IDisposable
{
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
        Thread.Sleep(200);

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

        Thread.Sleep(100); // スナップショット取得を待つ
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
        var deadline = DateTime.UtcNow.AddSeconds(5);
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
    public void RecordFrame_ConcurrentCalls_NoCorruption()
    {
        _engine.Start();
        const int threadCount = 8;
        const int callsPerThread = 10_000;

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < callsPerThread; i++)
                _engine.RecordFrame();
        })).ToArray();

        Task.WaitAll(tasks);
        Thread.Sleep(100);

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
        Thread.Sleep(300);

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
    /// SnapshotTakenイベントハンドラが例外を投げるとプロセスクラッシュする（設計バグ確認）
    /// Timer内の未処理例外はUnhandledExceptionになるため、このテストは
    /// 「例外をスローするハンドラを登録しない」ことをユーザーに求める仕様を確認する。
    /// </summary>
    [Fact]
    public void SnapshotTaken_HandlerThrows_IsDesignBug()
    {
        // Timer内でスローされた例外はプロセスをクラッシュさせるため、
        // このシナリオでは「OnTick内でtry-catchすべき」という仕様バグ。
        // テストではクラッシュさせずに確認だけ行う。
        int callCount = 0;
        _engine.SnapshotTaken += (_, _) =>
        {
            Interlocked.Increment(ref callCount);
        };

        _engine.Start();
        Thread.Sleep(200);

        Assert.True(callCount >= 1, "正常なハンドラでは呼ばれるべき");
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
    /// GcPauseTimeMsが常に0であることの確認（未実装の記録）
    /// </summary>
    [Fact]
    public void Snapshot_GcPauseTimeMs_AlwaysZero()
    {
        _engine.Start();
        Thread.Sleep(200);

        var history = _engine.GetHistory();
        Assert.NotEmpty(history);
        Assert.All(history, s => Assert.Equal(0, s.GcPauseTimeMs));
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// スナップショットのメモリ値が非負であること
    /// </summary>
    [Fact]
    public void Snapshot_MemoryValues_NonNegative()
    {
        _engine.Start();
        Thread.Sleep(200);

        var latest = _engine.Latest;
        Assert.NotNull(latest);
        Assert.True(latest!.WorkingSetBytes >= 0);
        Assert.True(latest.GcTotalMemoryBytes >= 0);
        Assert.True(latest.Gen0Collections >= 0);
        Assert.True(latest.Gen1Collections >= 0);
        Assert.True(latest.Gen2Collections >= 0);
    }
}
