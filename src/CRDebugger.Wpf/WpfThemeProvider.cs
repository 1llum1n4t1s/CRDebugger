using CRDebugger.Core.Abstractions;
using Microsoft.Win32;

namespace CRDebugger.Wpf;

/// <summary>
/// Windows レジストリからダークモード設定を検出する IThemeProvider 実装
/// </summary>
public sealed class WpfThemeProvider : IThemeProvider
{
    private const string RegistryKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    private Action<bool>? _callback;
    private bool _monitoring;

    public bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            // 0 = ダークモード, 1 = ライトモード
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return false;
        }
    }

    public void StartMonitoring(Action<bool> onSystemThemeChanged)
    {
        _callback = onSystemThemeChanged;

        if (!_monitoring)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _monitoring = true;
        }
    }

    public void StopMonitoring()
    {
        _callback = null;

        if (_monitoring)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _monitoring = false;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            var isDark = IsSystemDarkMode();
            _callback?.Invoke(isDark);
        }
    }
}
