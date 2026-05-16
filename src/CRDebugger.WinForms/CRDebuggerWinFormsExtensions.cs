using CRDebugger.Core;

namespace CRDebugger.WinForms;

/// <summary>
/// CRDebuggerOptionsにWinForms UIレイヤーを登録する拡張メソッドを提供するクラス。
/// UseWinForms() を呼ぶことで、WinForms向けのウィンドウ・UIスレッド・テーマプロバイダーが
/// オプションに自動登録される。
/// </summary>
public static class CRDebuggerWinFormsExtensions
{
    /// <summary>
    /// CRDebuggerOptionsにWinForms用の実装を登録する拡張メソッド。
    /// <see cref="WinFormsDebuggerWindow"/>、<see cref="WinFormsUiThread"/>、
    /// <see cref="WinFormsThemeProvider"/> をそれぞれ設定する。
    /// </summary>
    /// <param name="options">設定対象の <see cref="CRDebuggerOptions"/> インスタンス。</param>
    /// <returns>メソッドチェーン用に同じ <see cref="CRDebuggerOptions"/> インスタンスを返す。</returns>
    public static CRDebuggerOptions UseWinForms(this CRDebuggerOptions options)
    {
        // WinForms用デバッガーウィンドウ実装を登録
        options.Window = new WinFormsDebuggerWindow();
        // WinForms用UIスレッドマーシャリング実装を登録
        options.UiThread = new WinFormsUiThread();
        // Windows OSのテーマ検出・監視プロバイダーを登録
        options.ThemeProvider = new WinFormsThemeProvider();
        return options;
    }

    /// <summary>
    /// WinForms 向けの初期設定を適用し、追加のオプションをコールバックで設定して CRDebugger を初期化する便利メソッド。
    /// Avalonia / WPF と統一されたエントリポイントを提供する。
    /// </summary>
    /// <param name="configure">追加の設定を行うコールバック</param>
    public static void Initialize(Action<CRDebuggerOptions> configure)
    {
        // WinForms 用の初期設定を適用した options を生成する
        var options = new CRDebuggerOptions();
        options.UseWinForms();
        // 呼び出し元からの追加設定を反映する
        configure(options);
        // CRDebugger 本体を初期化する
        Core.CRDebugger.Initialize(options);
    }
}
