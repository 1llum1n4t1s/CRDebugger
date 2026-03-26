using CRDebugger.Core.Theming;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// デバッガーウィンドウのルートViewModel
/// </summary>
public sealed class DebuggerViewModel : ViewModelBase
{
    private CRTab _selectedTab;
    private ThemeColors _themeColors;
    private readonly HashSet<CRTab> _disabledTabs = new();

    public SystemInfoViewModel SystemInfo { get; }
    public ConsoleViewModel Console { get; }
    public OptionsViewModel Options { get; }
    public ProfilerViewModel Profiler { get; }
    public BugReporterViewModel BugReporter { get; }
    public ThemeManager ThemeManager { get; }

    /// <summary>タブの有効/無効状態変更時に発火</summary>
    public event EventHandler? TabStateChanged;

    public CRTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_disabledTabs.Contains(value)) return; // 無効タブは選択不可
            SetProperty(ref _selectedTab, value);
        }
    }

    public ThemeColors ThemeColors
    {
        get => _themeColors;
        set => SetProperty(ref _themeColors, value);
    }

    /// <summary>指定タブが有効かどうか</summary>
    public bool IsTabEnabled(CRTab tab) => !_disabledTabs.Contains(tab);

    /// <summary>有効なタブの一覧</summary>
    public IReadOnlyList<CRTab> EnabledTabs =>
        Enum.GetValues<CRTab>().Where(t => !_disabledTabs.Contains(t)).ToList();

    /// <summary>タブの有効/無効を設定</summary>
    public void SetTabEnabled(CRTab tab, bool enabled)
    {
        if (enabled)
            _disabledTabs.Remove(tab);
        else
            _disabledTabs.Add(tab);

        // 現在選択中のタブが無効化された場合、最初の有効タブに切替
        if (!enabled && _selectedTab == tab)
        {
            var firstEnabled = Enum.GetValues<CRTab>().FirstOrDefault(t => !_disabledTabs.Contains(t));
            SelectedTab = firstEnabled;
        }

        TabStateChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(EnabledTabs));
    }

    public DebuggerViewModel(
        SystemInfoViewModel systemInfo,
        ConsoleViewModel console,
        OptionsViewModel options,
        ProfilerViewModel profiler,
        BugReporterViewModel bugReporter,
        ThemeManager themeManager,
        CRTab defaultTab,
        IEnumerable<CRTab>? disabledTabs = null)
    {
        SystemInfo = systemInfo;
        Console = console;
        Options = options;
        Profiler = profiler;
        BugReporter = bugReporter;
        ThemeManager = themeManager;
        _themeColors = themeManager.CurrentColors;

        if (disabledTabs != null)
        {
            foreach (var tab in disabledTabs)
                _disabledTabs.Add(tab);
        }

        _selectedTab = _disabledTabs.Contains(defaultTab)
            ? Enum.GetValues<CRTab>().FirstOrDefault(t => !_disabledTabs.Contains(t))
            : defaultTab;

        themeManager.ThemeChanged += (_, colors) => ThemeColors = colors;
    }
}
