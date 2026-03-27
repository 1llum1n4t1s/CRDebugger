using Avalonia.Threading;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Avalonia;

/// <summary>
/// <see cref="IDebuggerWindow"/> の Avalonia 実装。
/// デバッガーウィンドウの生成・表示・非表示・テーマ適用を担当する。
/// </summary>
public sealed class AvaloniaDebuggerWindow : IDebuggerWindow
{
    /// <summary>実際に表示する Avalonia ウィンドウのインスタンス（未表示時は null）</summary>
    private Windows.DebuggerWindow? _window;

    /// <summary>
    /// ウィンドウが現在表示中かどうかを取得する。
    /// </summary>
    public bool IsVisible => _window?.IsVisible ?? false;

    /// <summary>
    /// 指定した ViewModel でデバッガーウィンドウを表示する。
    /// ウィンドウが未作成または非表示の場合は新たに生成して DataContext を設定する。
    /// </summary>
    /// <param name="viewModel">ウィンドウにバインドする DebuggerViewModel</param>
    public void Show(DebuggerViewModel viewModel)
    {
        // ウィンドウが存在しない、または既に閉じられている場合は新規作成する
        if (_window == null || !_window.IsVisible)
        {
            _window = new Windows.DebuggerWindow { DataContext = viewModel };
        }
        _window.Show();
    }

    /// <summary>
    /// デバッガーウィンドウを非表示にする。
    /// ウィンドウが存在しない場合は何もしない。
    /// </summary>
    public void Hide() => _window?.Hide();

    /// <summary>
    /// テーマカラーをウィンドウに適用する。
    /// Avalonia 実装では DebuggerViewModel.ThemeColors バインディング経由で自動適用されるため、ここでは何もしない。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報</param>
    public void ApplyTheme(ThemeColors colors)
    {
        // テーマは DebuggerViewModel.ThemeColors バインディング経由で適用される
    }

    /// <summary>
    /// ウィンドウのスクリーンショットを非同期で取得する。
    /// 現時点では未実装のため常に null を返す。
    /// </summary>
    /// <returns>PNG バイト配列。未実装のため常に null</returns>
    public Task<byte[]?> CaptureScreenshotAsync()
    {
        // スクリーンショットキャプチャのプレースホルダー
        return Task.FromResult<byte[]?>(null);
    }
}
