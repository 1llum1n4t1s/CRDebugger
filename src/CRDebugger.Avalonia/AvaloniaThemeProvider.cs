using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Avalonia;

/// <summary>
/// Avalonia の PlatformSettings を使った OS ダークモード検出
/// </summary>
public sealed class AvaloniaThemeProvider : IThemeProvider
{
    private Action<bool>? _callback;

    public bool IsSystemDarkMode()
    {
        var app = Application.Current;
        if (app == null) return false;

        var variant = app.PlatformSettings?.GetColorValues().ThemeVariant;
        return variant == PlatformThemeVariant.Dark;
    }

    public void StartMonitoring(Action<bool> onSystemThemeChanged)
    {
        _callback = onSystemThemeChanged;
        var app = Application.Current;
        if (app?.PlatformSettings != null)
        {
            app.PlatformSettings.ColorValuesChanged += OnColorValuesChanged;
        }
    }

    public void StopMonitoring()
    {
        var app = Application.Current;
        if (app?.PlatformSettings != null)
        {
            app.PlatformSettings.ColorValuesChanged -= OnColorValuesChanged;
        }
        _callback = null;
    }

    private void OnColorValuesChanged(object? sender, PlatformColorValues e)
    {
        var isDark = e.ThemeVariant == PlatformThemeVariant.Dark;
        if (_callback != null)
        {
            Dispatcher.UIThread.Post(() => _callback(isDark));
        }
    }
}
