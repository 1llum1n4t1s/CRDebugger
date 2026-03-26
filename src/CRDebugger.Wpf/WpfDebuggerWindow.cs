using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using CRDebugger.Wpf.Windows;

namespace CRDebugger.Wpf;

/// <summary>
/// WPF デバッガーウィンドウの IDebuggerWindow 実装
/// </summary>
public sealed class WpfDebuggerWindow : IDebuggerWindow
{
    private DebuggerWindow? _window;
    private DebuggerViewModel? _viewModel;

    public bool IsVisible => _window?.IsVisible ?? false;

    public void Show(DebuggerViewModel viewModel)
    {
        _viewModel = viewModel;

        if (_window == null || !_window.IsLoaded)
        {
            _window = new DebuggerWindow();
            _window.Closed += (_, _) => _window = null;
        }

        _window.DataContext = viewModel;
        _window.ApplyThemeColors(viewModel.ThemeColors);

        // テーマ変更を監視
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DebuggerViewModel.ThemeColors))
            {
                _window?.ApplyThemeColors(viewModel.ThemeColors);
            }
        };

        _window.Show();
        _window.Activate();
    }

    public void Hide()
    {
        _window?.Hide();
    }

    public void ApplyTheme(ThemeColors colors)
    {
        _window?.ApplyThemeColors(colors);
    }

    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        if (_window == null || !_window.IsVisible)
            return null;

        byte[]? result = null;
        await _window.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var bounds = VisualTreeHelper.GetDescendantBounds(_window);
                var width = (int)_window.ActualWidth;
                var height = (int)_window.ActualHeight;

                if (width <= 0 || height <= 0)
                    return;

                var dpi = VisualTreeHelper.GetDpi(_window);
                var renderTarget = new RenderTargetBitmap(
                    (int)(width * dpi.DpiScaleX),
                    (int)(height * dpi.DpiScaleY),
                    dpi.PixelsPerInchX,
                    dpi.PixelsPerInchY,
                    PixelFormats.Pbgra32);

                renderTarget.Render(_window);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                using var stream = new MemoryStream();
                encoder.Save(stream);
                result = stream.ToArray();
            }
            catch
            {
                // スクリーンショット取得に失敗した場合はnullを返す
            }
        });

        return result;
    }
}
