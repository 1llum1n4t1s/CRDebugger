using CRDebugger.Core;

namespace CRDebugger.WinForms;

/// <summary>
/// CRDebuggerOptionsにWinForms UIレイヤーを登録する拡張メソッド
/// </summary>
public static class CRDebuggerWinFormsExtensions
{
    public static CRDebuggerOptions UseWinForms(this CRDebuggerOptions options)
    {
        options.Window = new WinFormsDebuggerWindow();
        options.UiThread = new WinFormsUiThread();
        options.ThemeProvider = new WinFormsThemeProvider();
        return options;
    }
}
