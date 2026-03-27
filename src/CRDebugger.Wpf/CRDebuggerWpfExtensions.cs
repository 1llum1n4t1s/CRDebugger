using CRDebugger.Core;

namespace CRDebugger.Wpf;

/// <summary>
/// CRDebuggerOptions に WPF UI レイヤーを設定する拡張メソッド群
/// </summary>
public static class CRDebuggerWpfExtensions
{
    /// <summary>
    /// CRDebuggerOptions に WPF 向けの実装クラスを一括設定する
    /// </summary>
    /// <param name="options">設定を適用する CRDebuggerOptions インスタンス</param>
    /// <returns>チェーン呼び出し可能なように同じ options インスタンスを返す</returns>
    public static CRDebuggerOptions UseWpf(this CRDebuggerOptions options)
    {
        // WPF 用デバッガーウィンドウ実装を設定
        options.Window = new WpfDebuggerWindow();
        // WPF Dispatcher ベースの UI スレッド実装を設定
        options.UiThread = new WpfUiThread();
        // Windows レジストリからダークモードを検出するテーマプロバイダーを設定
        options.ThemeProvider = new WpfThemeProvider();
        return options;
    }
}
