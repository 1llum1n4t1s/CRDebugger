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
    long GcPauseTimeMs
);
