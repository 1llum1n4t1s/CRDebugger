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

    /// <summary>
    /// WPF 向けの初期設定を適用し、追加のオプションをコールバックで設定して CRDebugger を初期化する便利メソッド。
    /// Avalonia / WinForms と統一されたエントリポイントを提供する。
    /// </summary>
    /// <param name="configure">追加の設定を行うコールバック</param>
    public static void Initialize(Action<CRDebuggerOptions> configure)
    {
        // WPF 用の初期設定を適用した options を生成する
        var options = new CRDebuggerOptions();
        options.UseWpf();
        // 呼び出し元からの追加設定を反映する
        configure(options);
        // CRDebugger 本体を初期化する
        Core.CRDebugger.Initialize(options);
    }
}
