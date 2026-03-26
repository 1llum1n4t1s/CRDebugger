using CRDebugger.Core;

namespace CRDebugger.Avalonia;

/// <summary>
/// Avalonia UI フレームワークの登録拡張メソッド
/// </summary>
public static class CRDebuggerAvaloniaExtensions
{
    /// <summary>
    /// CRDebuggerOptions に Avalonia UI 実装を登録する
    /// </summary>
    public static CRDebuggerOptions UseAvalonia(this CRDebuggerOptions options)
    {
        options.Window = new AvaloniaDebuggerWindow();
        options.UiThread = new AvaloniaUiThread();
        options.ThemeProvider = new AvaloniaThemeProvider();
        return options;
    }

    /// <summary>
    /// CRDebugger を Avalonia UI で初期化するヘルパー
    /// </summary>
    public static void Initialize(Action<CRDebuggerOptions> configure)
    {
        var options = new CRDebuggerOptions();
        options.UseAvalonia();
        configure(options);
        Core.CRDebugger.Initialize(options);
    }
}
