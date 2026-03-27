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

    /// <summary>スナップショット履歴のリスト（最大 <see cref="MaxHistorySize"/> 件）</summary>
    private readonly List<ProfilerSnapshot> _history = new();

    /// <summary>履歴リストへのスレッドセーフアクセスを保証する排他ロックオブジェクト</summary>
    private readonly object _lock = new();

    /// <summary>FPS計算に使用するストップウォッチ。タイマーTickごとに経過時間を測定する</summary>
    private readonly Stopwatch _fpsStopwatch = new();

    /// <summary>GPU情報を取得するプロバイダー</summary>
    private readonly IGpuMonitor _gpuMonitor;

    /// <summary>直近のサンプリング間隔中に記録されたフレーム数（Interlocked で操作）</summary>
    private int _frameCount;

    /// <summary>最後に計算されたFPS推定値</summary>
    private double _lastFps;

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
        // FPS計測用ストップウォッチを起動
        _fpsStopwatch.Start();

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
        lock (_lock) { return _history.ToList(); }
    }

    /// <summary>
    /// 最新のスナップショット。<see cref="Start"/> 後に最初のサンプリングが完了するまでは <c>null</c>
    /// </summary>
    public ProfilerSnapshot? Latest
    {
        get
        {
            lock (_lock)
            {
                // 履歴がある場合は末尾の要素（最新）を返す
                return _history.Count > 0 ? _history[^1] : null;
            }
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
            GcPauseTimeMs: 0,
            GpuUsagePercent: gpuUsage,
            GpuDedicatedMemoryBytes: gpuDedicated,
            GpuSharedMemoryBytes: gpuShared,
            GpuTemperatureCelsius: gpuTemp,
            GpuDeviceName: gpuName
        );

        lock (_lock)
        {
            // 履歴に追加し、MaxHistorySize を超えた場合は最古エントリを削除
            _history.Add(snapshot);
            if (_history.Count > MaxHistorySize)
                _history.RemoveAt(0);
        }

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

    /// <inheritdoc/>
    public void Dispose()
    {
        // タイマーを停止・解放し、参照をnullにする
        _timer?.Dispose();
        _timer = null;
    }
}
