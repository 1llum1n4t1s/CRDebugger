namespace CRDebugger.Core.Profiler;

/// <summary>
/// イミュータブルなプロファイラースナップショット
/// </summary>
public sealed record ProfilerSnapshot(
    DateTimeOffset Timestamp,
    double FpsEstimate,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long GcTotalMemoryBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long GcPauseTimeMs,
    // GPU情報
    double GpuUsagePercent = 0,
    long GpuDedicatedMemoryBytes = 0,
    long GpuSharedMemoryBytes = 0,
    double GpuTemperatureCelsius = -1,
    string GpuDeviceName = "N/A"
);
