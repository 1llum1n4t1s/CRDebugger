namespace CRDebugger.Core.Abstractions;

/// <summary>
/// GPU使用率・メモリ・温度・デバイス名を監視するインターフェース。
/// プラットフォーム固有の実装（DirectX・NVML・OpenGL など）を注入可能にする。
/// GPU情報が取得できない環境では <see cref="NullGpuMonitor"/> をフォールバックとして使用する。
/// </summary>
public interface IGpuMonitor
{
    /// <summary>
    /// 現在のGPU使用率を取得する
    /// </summary>
    /// <returns>GPU使用率（0〜100%）</returns>
    double GetUsagePercent();

    /// <summary>
    /// GPU専用（VRAM）メモリ使用量を取得する
    /// </summary>
    /// <returns>専用メモリ使用量（バイト）</returns>
    long GetDedicatedMemoryBytes();

    /// <summary>
    /// GPUが共有するシステムメモリ使用量を取得する
    /// </summary>
    /// <returns>共有メモリ使用量（バイト）</returns>
    long GetSharedMemoryBytes();

    /// <summary>
    /// GPUコアの温度を取得する
    /// </summary>
    /// <returns>GPU温度（℃）。取得不可の場合は <c>-1</c></returns>
    double GetTemperatureCelsius();

    /// <summary>
    /// GPUデバイスの名称を取得する
    /// </summary>
    /// <returns>GPU名称文字列。取得不可の場合は <c>"N/A"</c></returns>
    string GetDeviceName();
}

/// <summary>
/// GPU情報が取得できない環境向けのフォールバック実装（Null Object パターン）。
/// GPU非搭載・ドライバー未インストール・権限不足の場合に使用する。
/// </summary>
public sealed class NullGpuMonitor : IGpuMonitor
{
    /// <inheritdoc/>
    public double GetUsagePercent() => 0;
    /// <inheritdoc/>
    public long GetDedicatedMemoryBytes() => 0;
    /// <inheritdoc/>
    public long GetSharedMemoryBytes() => 0;
    /// <inheritdoc/>
    public double GetTemperatureCelsius() => -1;
    /// <inheritdoc/>
    public string GetDeviceName() => "N/A";
}
