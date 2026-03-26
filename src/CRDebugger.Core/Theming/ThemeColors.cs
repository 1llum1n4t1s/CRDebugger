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

    /// <summary>モダンダークテーマ（アクリル効果対応）</summary>
    public static ThemeColors Dark { get; } = new()
    {
        Background = 0xFF1A1A2E,
        Surface = 0xFF232338,
        SurfaceAlt = 0xFF2D2D45,
        Primary = 0xFF7C8FFF,
        OnBackground = 0xFFF0F0F5,
        OnSurface = 0xFFC0C0D5,
        OnSurfaceMuted = 0xFF9090A5,
        Border = 0x10FFFFFF,
        LogDebug = 0xFF6CAEDD,
        LogInfo = 0xFFB0B0C0,
        LogWarning = 0xFFE8C44A,
        LogError = 0xFFE05252,
        Success = 0xFF4CAF50,
        SidebarBackground = 0xFF12121C,
        SidebarText = 0xFF9090A5,
        SelectedTab = 0xFF7C8FFF,
    };

    /// <summary>モダンライトテーマ</summary>
    public static ThemeColors Light { get; } = new()
    {
        Background = 0xFFF5F5FA,
        Surface = 0xFFFFFFFF,
        SurfaceAlt = 0xFFF0F0F5,
        Primary = 0xFF4A6CF7,
        OnBackground = 0xFF1A1A2E,
        OnSurface = 0xFF333344,
        OnSurfaceMuted = 0xFF707085,
        Border = 0x10000000,
        LogDebug = 0xFF1565C0,
        LogInfo = 0xFF555566,
        LogWarning = 0xFFE65100,
        LogError = 0xFFB71C1C,
        Success = 0xFF2E7D32,
        SidebarBackground = 0xFFE8E8F0,
        SidebarText = 0xFF505068,
        SelectedTab = 0xFF4A6CF7,
    };
}
