using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Core.Abstractions;

/// <summary>
/// UIフレームワーク層が実装するデバッガーウィンドウ抽象。
/// Avalonia・WPF・WinForms など各プラットフォームでこのインターフェースを実装し、
/// コア層からウィンドウの表示/非表示・テーマ適用・スクリーンショット取得を操作できるようにする。
/// </summary>
public interface IDebuggerWindow
{
    /// <summary>ウィンドウが現在表示中かどうか</summary>
    bool IsVisible { get; }

    /// <summary>
    /// デバッガーウィンドウを表示する
    /// </summary>
    /// <param name="viewModel">ウィンドウにバインドするルートViewModel</param>
    void Show(DebuggerViewModel viewModel);

    /// <summary>デバッガーウィンドウを非表示にする</summary>
    void Hide();

    /// <summary>
    /// テーマカラーをウィンドウ全体に適用する
    /// </summary>
    /// <param name="colors">適用するテーマカラーセット</param>
    void ApplyTheme(ThemeColors colors);

    /// <summary>
    /// ウィンドウのスクリーンショットを非同期でキャプチャする
    /// </summary>
    /// <returns>PNG形式のスクリーンショット画像データ。キャプチャ不可の場合は <c>null</c></returns>
    Task<byte[]?> CaptureScreenshotAsync();
}
