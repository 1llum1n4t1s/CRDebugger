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
}
