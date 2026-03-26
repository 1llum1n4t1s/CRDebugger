namespace CRDebugger.Core.Theming;

/// <summary>
/// テーマの管理と変更通知
/// </summary>
public sealed class ThemeManager
{
    private CRTheme _currentTheme;
    private bool _systemIsDark;

    public event EventHandler<ThemeColors>? ThemeChanged;

    public ThemeManager(CRTheme initialTheme = CRTheme.System)
    {
        _currentTheme = initialTheme;
    }

    public CRTheme CurrentTheme => _currentTheme;

    public ThemeColors CurrentColors => ResolveColors();

    public void SetTheme(CRTheme theme)
    {
        _currentTheme = theme;
        ThemeChanged?.Invoke(this, CurrentColors);
    }

    /// <summary>
    /// OS側のダークモード状態が変わった時に呼ぶ（UIフレームワーク層から）
    /// </summary>
    public void NotifySystemThemeChanged(bool isDark)
    {
        _systemIsDark = isDark;
        if (_currentTheme == CRTheme.System)
        {
            ThemeChanged?.Invoke(this, CurrentColors);
        }
    }

    private ThemeColors ResolveColors() => _currentTheme switch
    {
        CRTheme.Light => ThemeColors.Light,
        CRTheme.Dark => ThemeColors.Dark,
        _ => _systemIsDark ? ThemeColors.Dark : ThemeColors.Light
    };
}
