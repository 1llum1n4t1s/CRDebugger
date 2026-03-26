using Avalonia.Threading;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Avalonia;

/// <summary>
/// IDebuggerWindow の Avalonia 実装
/// </summary>
public sealed class AvaloniaDebuggerWindow : IDebuggerWindow
{
    private Windows.DebuggerWindow? _window;

    public bool IsVisible => _window?.IsVisible ?? false;

    public void Show(DebuggerViewModel viewModel)
    {
        if (_window == null || !_window.IsVisible)
        {
            _window = new Windows.DebuggerWindow { DataContext = viewModel };
        }
        _window.Show();
    }

    public void Hide() => _window?.Hide();

    public void ApplyTheme(ThemeColors colors)
    {
        // テーマは DebuggerViewModel.ThemeColors バインディング経由で適用される
    }

    public Task<byte[]?> CaptureScreenshotAsync()
    {
        // スクリーンショットキャプチャのプレースホルダー
        return Task.FromResult<byte[]?>(null);
    }
}
