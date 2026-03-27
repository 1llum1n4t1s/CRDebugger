namespace CRDebugger.Core.Profiler;

/// <summary>
/// 計測対象のリソースカテゴリを表すフラグ列挙型。
/// 複数カテゴリを組み合わせて使用できる。
/// </summary>
[Flags]
public enum MetricCategory
{
    /// <summary>カテゴリなし（初期値）</summary>
    None = 0,
    /// <summary>CPU時間（プロセッサ消費時間）</summary>
    CpuTime = 1,
    /// <summary>メモリ使用量（ヒープ割り当て量）</summary>
    Memory = 2,
    /// <summary>処理時間（ウォールクロック時間）</summary>
    Duration = 4,
    /// <summary>ネットワークI/O（送受信バイト数）</summary>
    NetworkIO = 8,
    /// <summary>ストレージI/O（読み書きバイト数）</summary>
    StorageIO = 16,
    /// <summary>GPU使用率（グラフィックスプロセッサ負荷）</summary>
    GpuUsage = 32,
    /// <summary>全カテゴリを含む組み合わせ値</summary>
    All = CpuTime | Memory | Duration | NetworkIO | StorageIO | GpuUsage
}

/// <summary>
/// 1回の計測結果を表すイミュータブルなレコード型。
/// ProfilingScope が Dispose された際に生成される。
/// </summary>
/// <param name="Timestamp">計測が完了した日時</param>
/// <param name="Duration">操作の処理時間（開始から終了までのウォールクロック時間）</param>
/// <param name="CpuTime">操作中に消費したCPU時間</param>
/// <param name="MemoryDeltaBytes">操作前後のGC管理メモリ増減量（バイト）。正値はメモリ増加を示す</param>
/// <param name="NetworkBytesRead">操作中に受信したネットワークバイト数</param>
/// <param name="NetworkBytesWritten">操作中に送信したネットワークバイト数</param>
/// <param name="StorageBytesRead">操作中に読み込んだストレージバイト数</param>
/// <param name="StorageBytesWritten">操作中に書き込んだストレージバイト数</param>
/// <param name="GpuUsagePercent">操作中のGPU使用率（%）。0〜100の範囲</param>
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
/// 特定ロジック単位の計測結果を集計するメトリクスクラス。
/// 複数スレッドから安全にアクセスできるよう内部ロックを使用する。
/// </summary>
public sealed class OperationMetrics
{
    /// <summary>スレッドセーフなアクセスを保証するための排他ロックオブジェクト</summary>
    private readonly object _lock = new();

    /// <summary>直近サンプルを保持するリスト（最大 MaxSamples 件）</summary>
    private readonly List<OperationSample> _samples = new();

    /// <summary>保持するサンプルの最大件数。超過した場合は古いものから削除される</summary>
    private const int MaxSamples = 500;

    /// <summary>操作を識別する名前</summary>
    public string OperationName { get; }

    /// <summary>操作をグループ化するためのカテゴリタグ</summary>
    public string Category { get; }

    /// <summary>この操作が呼び出された累計回数</summary>
    public long InvocationCount { get; private set; }

    /// <summary>最後に記録されたサンプル。まだ記録がない場合は <c>null</c></summary>
    public OperationSample? LastSample { get; private set; }

    // ── 集計値 ──

    /// <summary>全サンプルの処理時間の合計</summary>
    public TimeSpan TotalDuration { get; private set; }
    /// <summary>全サンプルのCPU時間の合計</summary>
    public TimeSpan TotalCpuTime { get; private set; }
    /// <summary>全サンプルのメモリ増減量の合計（バイト）</summary>
    public long TotalMemoryDelta { get; private set; }
    /// <summary>全サンプルのネットワーク受信バイト数の合計</summary>
    public long TotalNetworkBytesRead { get; private set; }
    /// <summary>全サンプルのネットワーク送信バイト数の合計</summary>
    public long TotalNetworkBytesWritten { get; private set; }
    /// <summary>全サンプルのストレージ読み込みバイト数の合計</summary>
    public long TotalStorageBytesRead { get; private set; }
    /// <summary>全サンプルのストレージ書き込みバイト数の合計</summary>
    public long TotalStorageBytesWritten { get; private set; }

    // ── 最大値 ──

    /// <summary>記録されたサンプルの中で最も長い処理時間</summary>
    public TimeSpan MaxDuration { get; private set; }
    /// <summary>記録されたサンプルの中で最も長いCPU時間</summary>
    public TimeSpan MaxCpuTime { get; private set; }
    /// <summary>記録されたサンプルの中で最も大きいメモリ増加量（バイト）</summary>
    public long MaxMemoryDelta { get; private set; }

    /// <summary>
    /// 全サンプルの平均処理時間。呼び出し回数が 0 の場合は <see cref="TimeSpan.Zero"/> を返す
    /// </summary>
    public TimeSpan AverageDuration => InvocationCount > 0
        ? TimeSpan.FromTicks(TotalDuration.Ticks / InvocationCount)
        : TimeSpan.Zero;

    /// <summary>
    /// 全サンプルの平均CPU時間。呼び出し回数が 0 の場合は <see cref="TimeSpan.Zero"/> を返す
    /// </summary>
    public TimeSpan AverageCpuTime => InvocationCount > 0
        ? TimeSpan.FromTicks(TotalCpuTime.Ticks / InvocationCount)
        : TimeSpan.Zero;

    /// <summary>
    /// <see cref="OperationMetrics"/> のインスタンスを生成する
    /// </summary>
    /// <param name="operationName">この操作を識別する名前</param>
    /// <param name="category">操作をグループ化するカテゴリタグ（省略時は "General"）</param>
    public OperationMetrics(string operationName, string category = "General")
    {
        OperationName = operationName;
        Category = category;
    }

    /// <summary>
    /// 計測サンプルを記録し、集計値・最大値を更新する。
    /// このメソッドはスレッドセーフであり、複数スレッドから同時に呼び出せる。
    /// </summary>
    /// <param name="sample">記録するサンプルデータ</param>
    internal void RecordSample(OperationSample sample)
    {
        lock (_lock)
        {
            // 呼び出し回数をインクリメントし、最新サンプルを更新
            InvocationCount++;
            LastSample = sample;

            // 各指標の累計値を加算
            TotalDuration += sample.Duration;
            TotalCpuTime += sample.CpuTime;
            TotalMemoryDelta += sample.MemoryDeltaBytes;
            TotalNetworkBytesRead += sample.NetworkBytesRead;
            TotalNetworkBytesWritten += sample.NetworkBytesWritten;
            TotalStorageBytesRead += sample.StorageBytesRead;
            TotalStorageBytesWritten += sample.StorageBytesWritten;

            // 各指標の最大値を更新（既存最大値より大きい場合のみ置き換え）
            if (sample.Duration > MaxDuration) MaxDuration = sample.Duration;
            if (sample.CpuTime > MaxCpuTime) MaxCpuTime = sample.CpuTime;
            if (sample.MemoryDeltaBytes > MaxMemoryDelta) MaxMemoryDelta = sample.MemoryDeltaBytes;

            // 直近サンプルをリストに追加し、MaxSamples を超えた場合は先頭（最古）を削除
            _samples.Add(sample);
            if (_samples.Count > MaxSamples)
                _samples.RemoveAt(0);
        }
    }

    /// <summary>
    /// 保持している直近サンプルの一覧を返す。
    /// 返却リストは呼び出し時点のスナップショットであり、以降の変更を反映しない。
    /// </summary>
    /// <returns>直近の計測サンプル一覧（最大 <c>MaxSamples</c> 件）</returns>
    public IReadOnlyList<OperationSample> GetRecentSamples()
    {
        // ロック中にコピーを返すことでスレッドセーフを維持
        lock (_lock) { return _samples.ToList(); }
    }

    /// <summary>
    /// 全集計値・サンプル履歴をリセットし、初期状態に戻す。
    /// このメソッドはスレッドセーフである。
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            // カウンターと最新サンプルをリセット
            InvocationCount = 0;
            LastSample = null;

            // 処理時間・CPU時間をゼロリセット
            TotalDuration = TimeSpan.Zero;
            TotalCpuTime = TimeSpan.Zero;

            // バイト系集計値をゼロリセット
            TotalMemoryDelta = 0;
            TotalNetworkBytesRead = 0;
            TotalNetworkBytesWritten = 0;
            TotalStorageBytesRead = 0;
            TotalStorageBytesWritten = 0;

            // 最大値をゼロリセット
            MaxDuration = TimeSpan.Zero;
            MaxCpuTime = TimeSpan.Zero;
            MaxMemoryDelta = 0;

            // サンプル履歴を全消去
            _samples.Clear();
        }
    }
}
