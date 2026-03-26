namespace CRDebugger.Core.Abstractions;

/// <summary>
/// OSテーマ検出をUIフレームワーク層が実装
/// </summary>
public interface IThemeProvider
{
    /// <summary>OSがダークモードかどうかを返す</summary>
    bool IsSystemDarkMode();

    /// <summary>OSテーマ変更を監視開始</summary>
    void StartMonitoring(Action<bool> onSystemThemeChanged);

    /// <summary>監視停止</summary>
    void StopMonitoring();
}
