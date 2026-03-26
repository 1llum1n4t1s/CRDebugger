using System.Runtime.InteropServices;
using CRDebugger.Core.Abstractions;
using Microsoft.Win32;

namespace CRDebugger.WinForms;

/// <summary>
/// Windows OSのダークモード検出とテーマ変更監視
/// レジストリ AppsUseLightTheme の値で判定する
/// </summary>
public sealed class WinFormsThemeProvider : IThemeProvider, IDisposable
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    private System.Threading.Timer? _pollingTimer;
    private Action<bool>? _callback;
    private bool _lastKnownDarkMode;

    public bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            if (value is int intValue)
                return intValue == 0; // 0 = ダークモード
        }
        catch
        {
            // レジストリアクセス失敗時はライトモードとみなす
        }

        return false;
    }

    public void StartMonitoring(Action<bool> onSystemThemeChanged)
    {
        _callback = onSystemThemeChanged;
        _lastKnownDarkMode = IsSystemDarkMode();

        // レジストリ変更イベントが信頼性に欠けるため、ポーリングで監視
        _pollingTimer = new System.Threading.Timer(
            PollThemeChange,
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
    }

    public void StopMonitoring()
    {
        _pollingTimer?.Dispose();
        _pollingTimer = null;
        _callback = null;
    }

    private void PollThemeChange(object? state)
    {
        var currentDarkMode = IsSystemDarkMode();
        if (currentDarkMode != _lastKnownDarkMode)
        {
            _lastKnownDarkMode = currentDarkMode;
            _callback?.Invoke(currentDarkMode);
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
