using System.Diagnostics;

namespace CRDebugger.Core.Profiler;

/// <summary>
/// usingパターンでロジック単位の計測を行うスコープ。
/// 使用例:
/// <code>
/// using (CRDebugger.Profile("データベースクエリ"))
/// {
///     await db.QueryAsync(...);
/// }
/// </code>
/// </summary>
public sealed class ProfilingScope : IDisposable
{
    private readonly OperationTracker _tracker;
    private readonly string _operationName;
    private readonly string _category;
    private readonly Stopwatch _stopwatch;
    private readonly TimeSpan _startCpuTime;
    private readonly long _startMemory;
    private readonly long _startNetworkRead;
    private readonly long _startNetworkWrite;
    private readonly long _startStorageRead;
    private readonly long _startStorageWrite;
    private bool _disposed;

    internal ProfilingScope(OperationTracker tracker, string operationName, string category)
    {
        _tracker = tracker;
        _operationName = operationName;
        _category = category;

        // 開始時の各メトリクスをキャプチャ
        _startCpuTime = GetProcessCpuTime();
        _startMemory = GC.GetTotalMemory(false);
        (_startNetworkRead, _startNetworkWrite) = tracker.GetNetworkCounters();
        (_startStorageRead, _startStorageWrite) = tracker.GetStorageCounters();

        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stopwatch.Stop();

        var endCpuTime = GetProcessCpuTime();
        var endMemory = GC.GetTotalMemory(false);
        var (endNetRead, endNetWrite) = _tracker.GetNetworkCounters();
        var (endStoreRead, endStoreWrite) = _tracker.GetStorageCounters();

        var sample = new OperationSample(
            Timestamp: DateTimeOffset.Now,
            Duration: _stopwatch.Elapsed,
            CpuTime: endCpuTime - _startCpuTime,
            MemoryDeltaBytes: endMemory - _startMemory,
            NetworkBytesRead: endNetRead - _startNetworkRead,
            NetworkBytesWritten: endNetWrite - _startNetworkWrite,
            StorageBytesRead: endStoreRead - _startStorageRead,
            StorageBytesWritten: endStoreWrite - _startStorageWrite,
            GpuUsagePercent: 0 // GPU計測はプラットフォーム固有のため別途実装
        );

        _tracker.RecordSample(_operationName, _category, sample);
    }

    private static TimeSpan GetProcessCpuTime()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }
}
