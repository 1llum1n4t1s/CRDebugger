using System.Diagnostics;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Core.Profiler;

/// <summary>
/// 定期的にメモリ・GC・GPU・FPS 等のパフォーマンス情報をサンプリングするエンジン。
/// <see cref="Start"/> を呼ぶことでタイマーが起動し、指定間隔ごとに <see cref="ProfilerSnapshot"/> を生成する。
/// </summary>
public sealed class ProfilerEngine : IDisposable
{
    /// <summary>定期サンプリングを行うタイマー。<see cref="Start"/> 時に生成され、<see cref="Dispose"/> 時に解放される</summary>
    private System.Threading.Timer? _timer;

    /// <summary>スナップショット取得の間隔</summary>
    private readonly TimeSpan _interval;

    /// <summary>スナップショット履歴のキュー（最大 <see cref="MaxHistorySize"/> 件、O(1)でDequeue）</summary>
    private readonly Queue<ProfilerSnapshot> _history = new(MaxHistorySize);

    /// <summary>履歴リストへのスレッドセーフアクセスを保証する排他ロックオブジェクト</summary>
    private readonly object _lock = new();

    /// <summary>FPS計算に使用するストップウォッチ。タイマーTickごとに経過時間を測定する</summary>
    private readonly Stopwatch _fpsStopwatch = new();

    /// <summary>GPU情報を取得するプロバイダー</summary>
    private readonly IGpuMonitor _gpuMonitor;

    /// <summary>最新スナップショットのキャッシュ（Queue.Last() の O(n) 走査を回避）</summary>
    private ProfilerSnapshot? _latestSnapshot;

    /// <summary>直近のサンプリング間隔中に記録されたフレーム数（Interlocked で操作）</summary>
    private int _frameCount;

    /// <summary>最後に計算されたFPS推定値</summary>
    private double _lastFps;

    /// <summary>前回のCPU時間（CPU使用率計算用）</summary>
    private TimeSpan _previousCpuTime;

    /// <summary>前回のCPU時間タイムスタンプ（CPU使用率計算用）</summary>
    private DateTimeOffset _previousCpuTimestamp;

#if NET9_0_OR_GREATER
    /// <summary>前回のGCポーズ時間（差分計算でインターバル中のポーズ時間を求める）</summary>
    private TimeSpan _previousGcPauseTime;
#endif

    /// <summary>スナップショット履歴の最大保持件数</summary>
    public const int MaxHistorySize = 120;

    /// <summary>スナップショットが取得されるたびに発火するイベント</summary>
    public event EventHandler<ProfilerSnapshot>? SnapshotTaken;

    /// <summary>ロジック単位のプロファイリングを管理するトラッカー</summary>
    public OperationTracker Operations { get; } = new();

    /// <summary>
    /// <see cref="ProfilerEngine"/> のインスタンスを生成する
    /// </summary>
    /// <param name="interval">サンプリング間隔（省略時は 500ms）</param>
    /// <param name="gpuMonitor">GPU監視プロバイダー（省略時は何も取得しない <c>NullGpuMonitor</c>）</param>
    public ProfilerEngine(TimeSpan? interval = null, IGpuMonitor? gpuMonitor = null)
    {
        _interval = interval ?? TimeSpan.FromMilliseconds(500);
        _gpuMonitor = gpuMonitor ?? new NullGpuMonitor();
    }

    /// <summary>
    /// プロファイラーの定期サンプリングを開始する。
    /// FPS計測用ストップウォッチを起動し、指定間隔で <see cref="OnTick"/> が呼ばれるタイマーを設定する。
    /// </summary>
    public void Start()
    {
        // 二重 Start() 時の前回タイマーリークを防止する
        _timer?.Dispose();

        _fpsStopwatch.Start();

        // CPU使用率計算の基準値を初期化
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            _previousCpuTime = process.TotalProcessorTime;
        }
        catch { _previousCpuTime = TimeSpan.Zero; }
        _previousCpuTimestamp = DateTimeOffset.Now;

#if NET9_0_OR_GREATER
        // GCポーズ時間の基準値を初期化（.NET 9+ で利用可能）
        _previousGcPauseTime = GC.GetTotalPauseDuration();
#endif

        // 初回は即座に実行し、以降は _interval ごとに OnTick を呼び出す
        _timer = new System.Threading.Timer(OnTick, null, TimeSpan.Zero, _interval);
    }

    /// <summary>
    /// フレームカウントを 1 増加させる。
    /// アプリ側のレンダリングループから毎フレーム呼び出すことで FPS 推定が有効になる。
    /// </summary>
    public void RecordFrame()
    {
        // Interlocked.Increment でスレッドセーフにインクリメント
        Interlocked.Increment(ref _frameCount);
    }

    /// <summary>
    /// スナップショットの履歴一覧を返す。
    /// 返却リストは呼び出し時点のスナップショットであり、以降の変更を反映しない。
    /// </summary>
    /// <returns>取得済みスナップショットのリスト（最大 <see cref="MaxHistorySize"/> 件）</returns>
    public IReadOnlyList<ProfilerSnapshot> GetHistory()
    {
        // ロック中にコピーを返すことでスレッドセーフを維持
        lock (_lock) { return [.. _history]; }
    }

    /// <summary>
    /// CPU使用率の時系列履歴を返す。
    /// </summary>
    /// <returns>CPU使用率のリスト（最大 <see cref="MaxHistorySize"/> 件）</returns>
    public IReadOnlyList<double> GetCpuHistory()
    {
        lock (_lock) { return _history.Select(s => s.CpuUsagePercent).ToArray(); }
    }

    /// <summary>
    /// 最新のスナップショット。<see cref="Start"/> 後に最初のサンプリングが完了するまでは <c>null</c>
    /// </summary>
    public ProfilerSnapshot? Latest
    {
        get
        {
            lock (_lock) { return _latestSnapshot; }
        }
    }

    /// <summary>
    /// GCを強制的に実行し、ファイナライザーキューの処理を待機する。
    /// メモリリーク調査や強制的なメモリ解放が必要な場合に使用する。
    /// </summary>
    public void ForceGarbageCollection()
    {
        // 第1世代以降も含めた完全GCを2回実行（ファイナライザー起動後のオブジェクト回収のため2回）
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// タイマーのコールバックメソッド。指定間隔ごとに呼ばれ、スナップショットを生成・記録する。
    /// </summary>
    /// <param name="state">未使用のタイマー状態オブジェクト</param>
    private void OnTick(object? state)
    {
        // Timer のコールバック内で発生した未処理例外はプロセスをクラッシュさせるため、
        // OnTick 全体を try/catch で囲んでホスト側に逆流させない (#33)
        try
        {
            // FPS計算：経過時間中のフレーム数を測定し、フレームカウンターをリセット
            var elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
            if (elapsed > 0)
            {
                // フレーム数をアトミックに0リセットし、FPSを算出
                var frames = Interlocked.Exchange(ref _frameCount, 0);
                _lastFps = frames / elapsed;
                _fpsStopwatch.Restart();
            }

            // 現在のプロセス情報を取得（usingでリソースを確実に解放）
            using var process = Process.GetCurrentProcess();

            // CPU使用率計算：前回からの差分で算出
            var now = DateTimeOffset.Now;
            double cpuPercent = 0;
            try
            {
                var currentCpuTime = process.TotalProcessorTime;
                var cpuDelta = (currentCpuTime - _previousCpuTime).TotalMilliseconds;
                var timeDelta = (now - _previousCpuTimestamp).TotalMilliseconds;
                if (timeDelta > 0)
                {
                    cpuPercent = (cpuDelta / (timeDelta * Environment.ProcessorCount)) * 100;
                    cpuPercent = Math.Clamp(cpuPercent, 0, 100);
                }
                _previousCpuTime = currentCpuTime;
                _previousCpuTimestamp = now;
            }
            catch { /* CPU時間取得失敗時はデフォルト値0を使用 */ }

            // GPU情報取得（取得失敗時はデフォルト値を使用し、処理を継続する）
            double gpuUsage = 0;
            long gpuDedicated = 0, gpuShared = 0;
            double gpuTemp = -1;
            string gpuName = "N/A";
            try
            {
                gpuUsage = _gpuMonitor.GetUsagePercent();
                gpuDedicated = _gpuMonitor.GetDedicatedMemoryBytes();
                gpuShared = _gpuMonitor.GetSharedMemoryBytes();
                gpuTemp = _gpuMonitor.GetTemperatureCelsius();
                gpuName = _gpuMonitor.GetDeviceName();
            }
            catch { /* GPU情報取得失敗は無視（プラットフォーム非対応の場合もあるため） */ }

            // 取得した各指標をイミュータブルなスナップショットレコードにまとめる
            var snapshot = new ProfilerSnapshot(
                Timestamp: DateTimeOffset.Now,
                FpsEstimate: Math.Round(_lastFps, 1),
                WorkingSetBytes: process.WorkingSet64,
                PrivateMemoryBytes: process.PrivateMemorySize64,
                GcTotalMemoryBytes: GC.GetTotalMemory(false),
                Gen0Collections: GC.CollectionCount(0),
                Gen1Collections: GC.CollectionCount(1),
                Gen2Collections: GC.CollectionCount(2),
                    GcPauseTimeMs: GetGcPauseDeltaMs(),
                GpuUsagePercent: gpuUsage,
                GpuDedicatedMemoryBytes: gpuDedicated,
                GpuSharedMemoryBytes: gpuShared,
                GpuTemperatureCelsius: gpuTemp,
                GpuDeviceName: gpuName,
                CpuUsagePercent: Math.Round(cpuPercent, 1)
            );

            lock (_lock)
            {
                _history.Enqueue(snapshot);
                if (_history.Count > MaxHistorySize)
                    _history.Dequeue();
                _latestSnapshot = snapshot;
            }

            // OperationTracker のネットワーク／ストレージカウンタキャッシュを最新化する (#22)。
            // Snapshot 生成後に行うことで、初回 Tick が OS API 列挙でブロックして Snapshot 生成が遅延するのを防ぐ。
            // NetworkInterface.GetAllNetworkInterfaces() は初回呼び出しが重いため、計測の主目的（Snapshot）を優先する。
            try { Operations?.UpdateCounterSnapshot(); } catch { /* キャッシュ更新失敗は次回 Tick で再試行 */ }

            try
            {
                // スナップショット取得完了を通知（イベントハンドラの例外がタイマースレッドをクラッシュさせないようキャッチ）
                SnapshotTaken?.Invoke(this, snapshot);
            }
            catch
            {
                // Timer コールバック内の未処理例外はプロセスをクラッシュさせるため、ここで必ずキャッチする
            }
        }
        catch
        {
            // OnTick の予期しない例外は CRDebugger 哲学に従いホスト側に逆流させない (#33)。
            // ここで握りつぶすことで Timer スレッドの継続性を保証する
        }
    }

    /// <summary>
    /// サンプリング間隔中のGCポーズ時間（ミリ秒）を返す。
    /// .NET 9+ では GC.GetTotalPauseDuration() の差分、それ以前は常に 0。
    /// </summary>
    private long GetGcPauseDeltaMs()
    {
#if NET9_0_OR_GREATER
        var current = GC.GetTotalPauseDuration();
        var delta = (long)(current - _previousGcPauseTime).TotalMilliseconds;
        _previousGcPauseTime = current;
        return delta;
#else
        return 0;
#endif
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public void Dispose()
    {
        // Timer.Dispose(WaitHandle) を使い、進行中のコールバックが完了するまで待ってから解放する (#32)。
        // 引数なしの Dispose だとコールバック実行中でも即座にリターンしてしまい、
        // Dispose 後に OnTick の途中処理が走って状態破壊や ObjectDisposedException が発生するリスクがある。
        var timer = _timer;
        _timer = null;
        if (timer != null)
        {
            try
            {
                using var waitHandle = new ManualResetEvent(false);
                if (timer.Dispose(waitHandle))
                {
                    // Dispose(WaitHandle) は true を返した場合のみシグナルされる
                    waitHandle.WaitOne();
                }
            }
            catch
            {
                // Dispose 中の例外は呼び出し元（ホストアプリ）に伝播させない
            }
        }
    }
}
