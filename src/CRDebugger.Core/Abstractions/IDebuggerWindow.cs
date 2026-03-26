using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Core.Abstractions;

/// <summary>
/// UIフレームワーク層が実装するデバッガーウィンドウ抽象
/// </summary>
public interface IDebuggerWindow
{
    bool IsVisible { get; }
    void Show(DebuggerViewModel viewModel);
    void Hide();
    void ApplyTheme(ThemeColors colors);
    Task<byte[]?> CaptureScreenshotAsync();
}
