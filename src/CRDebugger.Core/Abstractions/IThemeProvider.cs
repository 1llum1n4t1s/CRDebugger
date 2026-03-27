namespace CRDebugger.Core.Abstractions;

/// <summary>
/// OSレベルのテーマ（ダーク/ライト）検出と変更監視を提供するインターフェース。
/// UIフレームワーク層がOS固有のAPI（Windows: Registry/WinRT、Avalonia: PlatformSettings など）を使って実装する。
/// <c>CRTheme.System</c> 選択時にOSのテーマ設定に自動追従するために使用される。
/// </summary>
public interface IThemeProvider
{
    /// <summary>
    /// OSが現在ダークモードに設定されているかどうかを返す
    /// </summary>
    /// <returns>ダークモードの場合 <c>true</c>、ライトモードの場合 <c>false</c></returns>
    bool IsSystemDarkMode();

    /// <summary>
    /// OSテーマ変更の監視を開始する。
    /// テーマが切り替わるたびにコールバックが呼び出される。
    /// </summary>
    /// <param name="onSystemThemeChanged">テーマ変更時のコールバック。ダークモードに変更された場合は <c>true</c>、ライトモードの場合は <c>false</c> が渡される</param>
    void StartMonitoring(Action<bool> onSystemThemeChanged);

    /// <summary>
    /// OSテーマ変更の監視を停止する。
    /// アプリケーション終了時やテーマ自動追従を無効化する際に呼び出す。
    /// </summary>
    void StopMonitoring();
}
