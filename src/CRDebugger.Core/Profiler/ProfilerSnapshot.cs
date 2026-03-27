namespace CRDebugger.Core.Profiler;

/// <summary>
/// 特定時点のシステムパフォーマンス情報を保持するイミュータブルなスナップショットレコード。
/// <see cref="ProfilerEngine"/> が定期サンプリングするたびに生成される。
/// </summary>
/// <param name="Timestamp">スナップショットを取得した日時</param>
/// <param name="FpsEstimate">直前のサンプリング間隔中の推定FPS（小数点1桁まで）</param>
/// <param name="WorkingSetBytes">プロセスのワーキングセットサイズ（バイト）。物理メモリの実使用量を示す</param>
/// <param name="PrivateMemoryBytes">プロセスのプライベートメモリサイズ（バイト）。他プロセスと共有されないメモリ量</param>
/// <param name="GcTotalMemoryBytes">GCが管理するヒープメモリの合計（バイト）</param>
/// <param name="Gen0Collections">Generation 0 のGC実行回数（累計）</param>
/// <param name="Gen1Collections">Generation 1 のGC実行回数（累計）</param>
/// <param name="Gen2Collections">Generation 2 のGC実行回数（累計）。フルGCの指標となる</param>
/// <param name="GcPauseTimeMs">GCポーズ時間（ミリ秒）。現在は常に 0（将来的な実装のためのプレースホルダー）</param>
/// <param name="GpuUsagePercent">GPU使用率（%）。取得できない場合は 0</param>
/// <param name="GpuDedicatedMemoryBytes">GPU専用メモリの使用量（バイト）。取得できない場合は 0</param>
/// <param name="GpuSharedMemoryBytes">CPUと共有されるGPUメモリの使用量（バイト）。取得できない場合は 0</param>
/// <param name="GpuTemperatureCelsius">GPU温度（℃）。取得できない場合は -1</param>
/// <param name="GpuDeviceName">GPUデバイス名。取得できない場合は "N/A"</param>
/// <param name="CpuUsagePercent">プロセスCPU使用率（%）。0〜100の範囲。取得できない場合は 0</param>
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
    // GPU情報（プラットフォームによっては取得不可）
    double GpuUsagePercent = 0,
    long GpuDedicatedMemoryBytes = 0,
    long GpuSharedMemoryBytes = 0,
    double GpuTemperatureCelsius = -1,
    string GpuDeviceName = "N/A",
    // プロセスCPU使用率（0〜100%）
    double CpuUsagePercent = 0
);
