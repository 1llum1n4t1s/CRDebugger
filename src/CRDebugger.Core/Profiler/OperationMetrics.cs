namespace CRDebugger.Core.Profiler;

/// <summary>
/// 計測対象のリソースカテゴリ
/// </summary>
[Flags]
public enum MetricCategory
{
    None = 0,
    CpuTime = 1,
    Memory = 2,
    Duration = 4,
    NetworkIO = 8,
    StorageIO = 16,
    GpuUsage = 32,
    All = CpuTime | Memory | Duration | NetworkIO | StorageIO | GpuUsage
}

/// <summary>
/// 1回の計測結果（イミュータブル）
/// </summary>
public sealed record OperationSample(
    DateTimeOffset Timestamp,
    TimeSpan Duration,
    TimeSpan CpuTime,
    long MemoryDeltaBytes,
    long NetworkBytesRead,
    long NetworkBytesWritten,
    long StorageBytesRead,
    long StorageBytesWritten,
    double GpuUsagePercent
);

/// <summary>
/// ロジック単位の集計メトリクス
/// </summary>
public sealed class OperationMetrics
{
    private readonly object _lock = new();
    private readonly List<OperationSample> _samples = new();
    private const int MaxSamples = 500;

    /// <summary>操作名</summary>
    public string OperationName { get; }

    /// <summary>カテゴリタグ（任意のグループ分け用）</summary>
    public string Category { get; }

    /// <summary>呼び出し回数</summary>
    public long InvocationCount { get; private set; }

    /// <summary>最後のサンプル</summary>
    public OperationSample? LastSample { get; private set; }

    // ── 集計値 ──

    /// <summary>合計処理時間</summary>
    public TimeSpan TotalDuration { get; private set; }
    /// <summary>合計CPU時間</summary>
    public TimeSpan TotalCpuTime { get; private set; }
    /// <summary>合計メモリ増減（バイト）</summary>
    public long TotalMemoryDelta { get; private set; }
    /// <summary>合計ネットワーク読み込み（バイト）</summary>
    public long TotalNetworkBytesRead { get; private set; }
    /// <summary>合計ネットワーク書き込み（バイト）</summary>
    public long TotalNetworkBytesWritten { get; private set; }
    /// <summary>合計ストレージ読み込み（バイト）</summary>
    public long TotalStorageBytesRead { get; private set; }
    /// <summary>合計ストレージ書き込み（バイト）</summary>
    public long TotalStorageBytesWritten { get; private set; }

    // ── 最大値 ──

    /// <summary>最大処理時間</summary>
    public TimeSpan MaxDuration { get; private set; }
    /// <summary>最大CPU時間</summary>
    public TimeSpan MaxCpuTime { get; private set; }
    /// <summary>最大メモリ増加（バイト）</summary>
    public long MaxMemoryDelta { get; private set; }

    /// <summary>平均処理時間</summary>
    public TimeSpan AverageDuration => InvocationCount > 0
        ? TimeSpan.FromTicks(TotalDuration.Ticks / InvocationCount)
        : TimeSpan.Zero;

    /// <summary>平均CPU時間</summary>
    public TimeSpan AverageCpuTime => InvocationCount > 0
        ? TimeSpan.FromTicks(TotalCpuTime.Ticks / InvocationCount)
        : TimeSpan.Zero;

    public OperationMetrics(string operationName, string category = "General")
    {
        OperationName = operationName;
        Category = category;
    }

    /// <summary>サンプルを記録</summary>
    internal void RecordSample(OperationSample sample)
    {
        lock (_lock)
        {
            InvocationCount++;
            LastSample = sample;

            // 集計値更新
            TotalDuration += sample.Duration;
            TotalCpuTime += sample.CpuTime;
            TotalMemoryDelta += sample.MemoryDeltaBytes;
            TotalNetworkBytesRead += sample.NetworkBytesRead;
            TotalNetworkBytesWritten += sample.NetworkBytesWritten;
            TotalStorageBytesRead += sample.StorageBytesRead;
            TotalStorageBytesWritten += sample.StorageBytesWritten;

            // 最大値更新
            if (sample.Duration > MaxDuration) MaxDuration = sample.Duration;
            if (sample.CpuTime > MaxCpuTime) MaxCpuTime = sample.CpuTime;
            if (sample.MemoryDeltaBytes > MaxMemoryDelta) MaxMemoryDelta = sample.MemoryDeltaBytes;

            // 直近サンプルを保持
            _samples.Add(sample);
            if (_samples.Count > MaxSamples)
                _samples.RemoveAt(0);
        }
    }

    /// <summary>直近サンプル一覧</summary>
    public IReadOnlyList<OperationSample> GetRecentSamples()
    {
        lock (_lock) { return _samples.ToList(); }
    }

    /// <summary>メトリクスをリセット</summary>
    public void Reset()
    {
        lock (_lock)
        {
            InvocationCount = 0;
            LastSample = null;
            TotalDuration = TimeSpan.Zero;
            TotalCpuTime = TimeSpan.Zero;
            TotalMemoryDelta = 0;
            TotalNetworkBytesRead = 0;
            TotalNetworkBytesWritten = 0;
            TotalStorageBytesRead = 0;
            TotalStorageBytesWritten = 0;
            MaxDuration = TimeSpan.Zero;
            MaxCpuTime = TimeSpan.Zero;
            MaxMemoryDelta = 0;
            _samples.Clear();
        }
    }
}
