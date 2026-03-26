using System.Diagnostics;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Core.Profiler;

/// <summary>
/// 定期的にメモリ・GC・パフォーマンス情報をサンプリングするエンジン
/// </summary>
public sealed class ProfilerEngine : IDisposable
{
    private System.Threading.Timer? _timer;
    private readonly TimeSpan _interval;
    private readonly List<ProfilerSnapshot> _history = new();
    private readonly object _lock = new();
    private readonly Stopwatch _fpsStopwatch = new();
    private readonly IGpuMonitor _gpuMonitor;
    private int _frameCount;
    private double _lastFps;

    public const int MaxHistorySize = 120;

    public event EventHandler<ProfilerSnapshot>? SnapshotTaken;

    /// <summary>ロジック単位のプロファイリングトラッカー</summary>
    public OperationTracker Operations { get; } = new();

    public ProfilerEngine(TimeSpan? interval = null, IGpuMonitor? gpuMonitor = null)
    {
        _interval = interval ?? TimeSpan.FromMilliseconds(500);
        _gpuMonitor = gpuMonitor ?? new NullGpuMonitor();
    }

    public void Start()
    {
        _fpsStopwatch.Start();
        _timer = new System.Threading.Timer(OnTick, null, TimeSpan.Zero, _interval);
    }

    /// <summary>
    /// フレームカウントを増加（アプリ側から呼ぶ）
    /// </summary>
    public void RecordFrame()
    {
        Interlocked.Increment(ref _frameCount);
    }

    public IReadOnlyList<ProfilerSnapshot> GetHistory()
    {
        lock (_lock) { return _history.ToList(); }
    }

    public ProfilerSnapshot? Latest
    {
        get
        {
            lock (_lock)
            {
                return _history.Count > 0 ? _history[^1] : null;
            }
        }
    }

    public void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void OnTick(object? state)
    {
        // FPS計算
        var elapsed = _fpsStopwatch.Elapsed.TotalSeconds;
        if (elapsed > 0)
        {
            var frames = Interlocked.Exchange(ref _frameCount, 0);
            _lastFps = frames / elapsed;
            _fpsStopwatch.Restart();
        }

        using var process = Process.GetCurrentProcess();

        // GPU情報取得
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
        catch { /* GPU情報取得失敗は無視 */ }

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
            _history.Add(snapshot);
            if (_history.Count > MaxHistorySize)
                _history.RemoveAt(0);
        }

        try
        {
            SnapshotTaken?.Invoke(this, snapshot);
        }
        catch
        {
            // Timer内の未処理例外はプロセスをクラッシュさせるため、ここでキャッチする
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
