using CRDebugger.Core.Theming;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// デバッガーウィンドウのルートViewModel。
/// 各タブ（システム情報・コンソール・オプション・プロファイラー・バグレポート）の
/// 子ViewModelを保持し、タブの有効/無効管理およびテーマカラーの伝播を担う。
/// </summary>
public sealed class DebuggerViewModel : ViewModelBase
{
    /// <summary>現在選択中のタブ（バッキングフィールド）</summary>
    private CRTab _selectedTab;

    /// <summary>現在のテーマカラーセット（バッキングフィールド）</summary>
    private ThemeColors _themeColors;

    /// <summary>無効化されているタブの集合。HashSetで高速な存在確認を実現</summary>
    private readonly HashSet<CRTab> _disabledTabs = new();

    /// <summary>Enum.GetValues のキャッシュ（毎回の配列生成を回避）</summary>
    private static readonly CRTab[] s_allTabs = Enum.GetValues<CRTab>();

    /// <summary>EnabledTabs のキャッシュ（SetTabEnabled時のみ再生成）</summary>
    private IReadOnlyList<CRTab> _enabledTabsCache = null!;

    /// <summary>システム情報タブのViewModel</summary>
    public SystemInfoViewModel SystemInfo { get; }

    /// <summary>コンソール（ログ）タブのViewModel</summary>
    public ConsoleViewModel Console { get; }

    /// <summary>オプション設定タブのViewModel</summary>
    public OptionsViewModel Options { get; }

    /// <summary>プロファイラータブのViewModel</summary>
    public ProfilerViewModel Profiler { get; }

    /// <summary>バグレポートタブのViewModel</summary>
    public BugReporterViewModel BugReporter { get; }

    /// <summary>テーマの切り替えおよびカラー管理を担うマネージャー</summary>
    public ThemeManager ThemeManager { get; }

    /// <summary>
    /// タブの有効/無効状態が変更されたときに発火するイベント。
    /// UIがタブヘッダーの表示を更新する契機として使用される。
    /// </summary>
    public event EventHandler? TabStateChanged;

    /// <summary>
    /// 現在選択中のタブ。
    /// 無効化されたタブを設定しようとした場合は無視される。
    /// </summary>
    public CRTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            // 無効化されているタブへの切替要求は拒否する
            if (_disabledTabs.Contains(value)) return;
            SetProperty(ref _selectedTab, value);
        }
    }

    /// <summary>
    /// 現在適用中のテーマカラーセット。
    /// <see cref="ThemeManager.ThemeChanged"/> イベントで自動更新される。
    /// </summary>
    public ThemeColors ThemeColors
    {
        get => _themeColors;
        set => SetProperty(ref _themeColors, value);
    }

    /// <summary>
    /// 指定したタブが現在有効かどうかを返す
    /// </summary>
    /// <param name="tab">確認対象のタブ</param>
    /// <returns>有効な場合は <c>true</c>、無効な場合は <c>false</c></returns>
    public bool IsTabEnabled(CRTab tab) => !_disabledTabs.Contains(tab);

    /// <summary>
    /// 現在有効なタブの一覧。
    /// UIのタブヘッダー生成に使用される。キャッシュ済みで毎アクセスのアロケーションなし。
    /// </summary>
    public IReadOnlyList<CRTab> EnabledTabs => _enabledTabsCache;

    /// <summary>
    /// 指定したタブの有効/無効状態を設定する。
    /// 現在選択中のタブが無効化された場合は、最初の有効タブに自動的に切り替わる。
    /// </summary>
    /// <param name="tab">対象のタブ</param>
    /// <param name="enabled"><c>true</c> で有効化、<c>false</c> で無効化</param>
    public void SetTabEnabled(CRTab tab, bool enabled)
    {
        if (enabled)
            // 有効化：無効タブセットから除外
            _disabledTabs.Remove(tab);
        else
            // 無効化：無効タブセットに追加
            _disabledTabs.Add(tab);

        // 現在選択中のタブが無効化された場合、最初の有効タブに自動切替
        if (!enabled && _selectedTab == tab)
        {
            var firstEnabled = s_allTabs.FirstOrDefault(t => !_disabledTabs.Contains(t));
            SelectedTab = firstEnabled;
        }

        // キャッシュを再生成してからUI通知を発火
        _enabledTabsCache = BuildEnabledTabs();

        // タブ状態変更を通知してUI側に再描画を促す
        TabStateChanged?.Invoke(this, EventArgs.Empty);
        // EnabledTabs プロパティ変更通知を発火
        OnPropertyChanged(nameof(EnabledTabs));
    }

    /// <summary>
    /// 現在の _disabledTabs から有効タブのリストを構築する
    /// </summary>
    private IReadOnlyList<CRTab> BuildEnabledTabs() =>
        s_allTabs.Where(t => !_disabledTabs.Contains(t)).ToArray();

    /// <summary>
    /// <see cref="DebuggerViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="systemInfo">システム情報タブのViewModel</param>
    /// <param name="console">コンソール（ログ）タブのViewModel</param>
    /// <param name="options">オプション設定タブのViewModel</param>
    /// <param name="profiler">プロファイラータブのViewModel</param>
    /// <param name="bugReporter">バグレポートタブのViewModel</param>
    /// <param name="themeManager">テーマカラーの管理・切替を担うマネージャー</param>
    /// <param name="defaultTab">ウィンドウ初回表示時に選択するタブ</param>
    /// <param name="disabledTabs">起動時から無効化しておくタブの一覧（省略可）</param>
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
        // 各タブのViewModelを保持
        SystemInfo = systemInfo;
        Console = console;
        Options = options;
        Profiler = profiler;
        BugReporter = bugReporter;
        ThemeManager = themeManager;
        // 初期テーマカラーをテーママネージャーから取得
        _themeColors = themeManager.CurrentColors;

        // 指定された無効タブをセットに追加
        if (disabledTabs != null)
        {
            foreach (var tab in disabledTabs)
                _disabledTabs.Add(tab);
        }

        // デフォルトタブが無効化されている場合は最初の有効タブを選択
        _selectedTab = _disabledTabs.Contains(defaultTab)
            ? s_allTabs.FirstOrDefault(t => !_disabledTabs.Contains(t))
            : defaultTab;

        // EnabledTabs キャッシュを初期化
        _enabledTabsCache = BuildEnabledTabs();

        // テーマ変更イベントを購読してThemeColorsプロパティを自動更新
        themeManager.ThemeChanged += (_, colors) => ThemeColors = colors;
    }
}
