namespace CRDebugger.Core.Theming;

/// <summary>
/// テーマカラーセット（ARGB形式）
/// </summary>
public readonly struct ThemeColors
{
    public uint Background { get; init; }
    public uint Surface { get; init; }
    public uint SurfaceAlt { get; init; }
    public uint Primary { get; init; }
    public uint OnBackground { get; init; }
    public uint OnSurface { get; init; }
    public uint OnSurfaceMuted { get; init; }
    public uint Border { get; init; }
    public uint LogDebug { get; init; }
    public uint LogInfo { get; init; }
    public uint LogWarning { get; init; }
    public uint LogError { get; init; }
    public uint Success { get; init; }
    public uint SidebarBackground { get; init; }
    public uint SidebarText { get; init; }
    public uint SelectedTab { get; init; }

    /// <summary>SRDebugger風ダークテーマ</summary>
    public static ThemeColors Dark { get; } = new()
    {
        Background = 0xFF1E1E2E,
        Surface = 0xFF2A2A3C,
        SurfaceAlt = 0xFF353548,
        Primary = 0xFF6C9BF2,
        OnBackground = 0xFFE0E0E0,
        OnSurface = 0xFFCCCCCC,
        OnSurfaceMuted = 0xFF888888,
        Border = 0xFF404060,
        LogDebug = 0xFF6CAEDD,
        LogInfo = 0xFFB0B0B0,
        LogWarning = 0xFFE8C44A,
        LogError = 0xFFE05252,
        Success = 0xFF4CAF50,
        SidebarBackground = 0xFF16161E,
        SidebarText = 0xFFAAAAAA,
        SelectedTab = 0xFF6C9BF2,
    };

    /// <summary>ライトテーマ</summary>
    public static ThemeColors Light { get; } = new()
    {
        Background = 0xFFF5F5F5,
        Surface = 0xFFFFFFFF,
        SurfaceAlt = 0xFFEEEEEE,
        Primary = 0xFF1976D2,
        OnBackground = 0xFF212121,
        OnSurface = 0xFF333333,
        OnSurfaceMuted = 0xFF757575,
        Border = 0xFFDDDDDD,
        LogDebug = 0xFF1565C0,
        LogInfo = 0xFF555555,
        LogWarning = 0xFFE65100,
        LogError = 0xFFB71C1C,
        Success = 0xFF2E7D32,
        SidebarBackground = 0xFFE8E8E8,
        SidebarText = 0xFF555555,
        SelectedTab = 0xFF1976D2,
    };
}
