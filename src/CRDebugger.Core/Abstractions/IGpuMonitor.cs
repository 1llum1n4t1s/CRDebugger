namespace CRDebugger.Core.Abstractions;

/// <summary>
/// GPU使用率を監視するインターフェース。
/// プラットフォーム固有の実装を注入可能。
/// </summary>
public interface IGpuMonitor
{
    /// <summary>現在のGPU使用率（0〜100%）</summary>
    double GetUsagePercent();

    /// <summary>GPU専用メモリ使用量（バイト）</summary>
    long GetDedicatedMemoryBytes();

    /// <summary>GPU共有メモリ使用量（バイト）</summary>
    long GetSharedMemoryBytes();

    /// <summary>GPU温度（℃、取得不可の場合-1）</summary>
    double GetTemperatureCelsius();

    /// <summary>GPU名称</summary>
    string GetDeviceName();
}

/// <summary>
/// GPU情報が取得できない場合のフォールバック実装
/// </summary>
public sealed class NullGpuMonitor : IGpuMonitor
{
    public double GetUsagePercent() => 0;
    public long GetDedicatedMemoryBytes() => 0;
    public long GetSharedMemoryBytes() => 0;
    public double GetTemperatureCelsius() => -1;
    public string GetDeviceName() => "N/A";
}
