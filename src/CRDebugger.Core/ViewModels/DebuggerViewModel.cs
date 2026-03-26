using CRDebugger.Core.Theming;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// デバッガーウィンドウのルートViewModel
/// </summary>
public sealed class DebuggerViewModel : ViewModelBase
{
    private CRTab _selectedTab;
    private ThemeColors _themeColors;

    public SystemInfoViewModel SystemInfo { get; }
    public ConsoleViewModel Console { get; }
    public OptionsViewModel Options { get; }
    public ProfilerViewModel Profiler { get; }
    public BugReporterViewModel BugReporter { get; }
    public ThemeManager ThemeManager { get; }

    public CRTab SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public ThemeColors ThemeColors
    {
        get => _themeColors;
        set => SetProperty(ref _themeColors, value);
    }

    public DebuggerViewModel(
        SystemInfoViewModel systemInfo,
        ConsoleViewModel console,
        OptionsViewModel options,
        ProfilerViewModel profiler,
        BugReporterViewModel bugReporter,
        ThemeManager themeManager,
        CRTab defaultTab)
    {
        SystemInfo = systemInfo;
        Console = console;
        Options = options;
        Profiler = profiler;
        BugReporter = bugReporter;
        ThemeManager = themeManager;
        _selectedTab = defaultTab;
        _themeColors = themeManager.CurrentColors;

        themeManager.ThemeChanged += (_, colors) => ThemeColors = colors;
    }
}
