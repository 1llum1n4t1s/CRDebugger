using CRDebugger.Core;

namespace CRDebugger.Wpf;

/// <summary>
/// CRDebuggerOptions に WPF UI レイヤーを設定する拡張メソッド
/// </summary>
public static class CRDebuggerWpfExtensions
{
    public static CRDebuggerOptions UseWpf(this CRDebuggerOptions options)
    {
        options.Window = new WpfDebuggerWindow();
        options.UiThread = new WpfUiThread();
        options.ThemeProvider = new WpfThemeProvider();
        return options;
    }
}
