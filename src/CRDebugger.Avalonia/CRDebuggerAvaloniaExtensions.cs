using CRDebugger.Core;

namespace CRDebugger.Avalonia;

/// <summary>
/// Avalonia UI フレームワーク向けの CRDebugger 登録拡張メソッド群。
/// <see cref="CRDebuggerOptions"/> に Avalonia 実装を一括登録するユーティリティを提供する。
/// </summary>
public static class CRDebuggerAvaloniaExtensions
{
    /// <summary>
    /// <see cref="CRDebuggerOptions"/> に Avalonia UI 実装を登録する。
    /// Window・UiThread・ThemeProvider の各プロパティに Avalonia 固有の実装をセットする。
    /// </summary>
    /// <param name="options">設定対象の CRDebuggerOptions</param>
    /// <returns>メソッドチェーン用に同じ options インスタンスを返す</returns>
    public static CRDebuggerOptions UseAvalonia(this CRDebuggerOptions options)
    {
        // Avalonia 実装のデバッガーウィンドウを登録する
        options.Window = new AvaloniaDebuggerWindow();
        // Avalonia の Dispatcher を使う UI スレッド実装を登録する
        options.UiThread = new AvaloniaUiThread();
        // Avalonia の PlatformSettings を使うテーマプロバイダーを登録する
        options.ThemeProvider = new AvaloniaThemeProvider();
        return options;
    }

    /// <summary>
    /// CRDebugger を Avalonia UI で初期化するヘルパーメソッド。
    /// Avalonia 実装を自動登録した上で、追加設定を <paramref name="configure"/> で受け付けて初期化する。
    /// </summary>
    /// <param name="configure">追加の設定を行うコールバック</param>
    public static void Initialize(Action<CRDebuggerOptions> configure)
    {
        // Avalonia 用の初期設定を適用した options を生成する
        var options = new CRDebuggerOptions();
        options.UseAvalonia();
        // 呼び出し元からの追加設定を反映する
        configure(options);
        // CRDebugger 本体を初期化する
        Core.CRDebugger.Initialize(options);
    }
}
