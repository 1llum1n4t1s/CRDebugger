using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Options;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// オプション設定画面のViewModel。
/// <see cref="OptionsEngine"/> が管理する全コンテナをスキャンし、
/// カテゴリ別にグループ化されたオプション項目を提供する。
/// 検索フィルタリングとカテゴリ折りたたみ機能を含む。
/// </summary>
public sealed class OptionsViewModel : ViewModelBase
{
    private readonly OptionsEngine _engine;
    private readonly IUiThread? _uiThread;

    /// <summary>カテゴリの展開/折りたたみ状態をリフレッシュ間で保持する辞書</summary>
    private readonly Dictionary<string, bool> _expandedState = new();

    private string _searchText = string.Empty;

    /// <summary>フィルタ前の完全なカテゴリ一覧（ApplyFilter のソース）</summary>
    private readonly List<OptionCategoryViewModel> _allCategories = new();

    /// <summary>前回の ApplyFilter で使用したクエリ（同値ガード用）</summary>
    private string _lastAppliedQuery = string.Empty;

    /// <summary>フィルタ適用後のカテゴリ一覧のバッキングフィールド</summary>
    private ObservableCollection<OptionCategoryViewModel> _filteredCategories = new();

    /// <summary>
    /// フィルタ適用後のカテゴリ一覧。UIのリストに直接バインドされる。
    /// ApplyFilter 時はコレクション丸ごと差し替えで単一 PropertyChanged 通知に最適化。
    /// </summary>
    public ObservableCollection<OptionCategoryViewModel> FilteredCategories
    {
        get => _filteredCategories;
        private set => SetProperty(ref _filteredCategories, value);
    }

    /// <summary>
    /// 検索テキスト。変更時に自動でフィルタリングを実行する。
    /// カテゴリ名・オプション表示名・アクションラベル・説明テキストが検索対象。
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set { SetProperty(ref _searchText, value); ApplyFilter(); }
    }

    /// <summary>オプション一覧を手動で再スキャンするコマンド</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>同期更新を行う互換用 ViewModel を生成する。</summary>
    /// <param name="engine">オプションの検出と変更通知を提供するエンジン</param>
    public OptionsViewModel(OptionsEngine engine)
        : this(engine, null, true)
    {
    }

    /// <summary>UI スレッドへ動的更新を配送する ViewModel を生成する。</summary>
    /// <param name="engine">オプションの検出と変更通知を提供するエンジン</param>
    /// <param name="uiThread">動的更新の配送先 UI スレッド</param>
    public OptionsViewModel(OptionsEngine engine, IUiThread uiThread)
        : this(engine, uiThread ?? throw new ArgumentNullException(nameof(uiThread)), true)
    {
    }

    private OptionsViewModel(OptionsEngine engine, IUiThread? uiThread, bool _)
    {
        _engine = engine;
        _uiThread = uiThread;
        RefreshCommand = new RelayCommand(Refresh);
        // 名前付きハンドラ経由で購読し、Dispose 時に -= で確実に解除可能にする
        _engine.ContainersChanged += OnContainersChanged;
        Refresh();
    }

    /// <summary>
    /// <see cref="OptionsEngine.ContainersChanged"/> イベントハンドラ。
    /// </summary>
    private void OnContainersChanged(object? sender, EventArgs e)
    {
        if (_uiThread == null)
            Refresh();
        else
            _uiThread.Invoke(Refresh);
    }

    /// <summary>
    /// エンジンを通じてコンテナを再スキャンし、カテゴリ一覧を更新する。
    /// 展開状態は保持される。
    /// </summary>
    public void Refresh()
    {
        foreach (var cat in _allCategories)
            _expandedState[cat.Name] = cat.IsExpanded;

        _allCategories.Clear();
        foreach (var cat in _engine.ScanAll())
        {
            var vm = new OptionCategoryViewModel(cat);
            if (_expandedState.TryGetValue(vm.Name, out var expanded))
                vm.IsExpanded = expanded;
            _allCategories.Add(vm);
        }

        // Refresh 後は必ずフィルタを再適用する（同値ガードをリセット）
        _lastAppliedQuery = "\0";
        ApplyFilter();
    }

    /// <summary>
    /// 検索テキストに基づいてフィルタリングを適用する。
    /// 同一クエリの連続呼び出しは早期リターンでスキップする。
    /// </summary>
    private void ApplyFilter()
    {
        var query = _searchText.Trim();

        // 同一クエリの連続呼び出しをスキップ（キーストローク最適化）
        if (query == _lastAppliedQuery) return;
        _lastAppliedQuery = query;

        // 一時リストに構築してから ObservableCollection を丸ごと差し替える
        // （Clear + N回 Add の N+1 通知 → 1 回の PropertyChanged に削減）
        var matched = new List<OptionCategoryViewModel>(_allCategories.Count);

        foreach (var cat in _allCategories)
        {
            if (string.IsNullOrEmpty(query))
            {
                cat.ApplyFilter(null);
                matched.Add(cat);
                continue;
            }

            // カテゴリ名ヒットでカテゴリ丸ごと表示
            if (cat.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                cat.ApplyFilter(null);
                matched.Add(cat);
                continue;
            }

            // アイテム/アクション単位でフィルタ
            cat.ApplyFilter(query);
            if (cat.FilteredItems.Count > 0 || cat.FilteredActions.Count > 0)
                matched.Add(cat);
        }

        FilteredCategories = new ObservableCollection<OptionCategoryViewModel>(matched);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // OptionsEngine のイベント購読を解除して GC ルートから切る
            _engine.ContainersChanged -= OnContainersChanged;
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// オプションカテゴリのViewModel。
/// 折りたたみ機能と検索フィルタリング機能を含む。
/// </summary>
public sealed class OptionCategoryViewModel : ViewModelBase
{
    private bool _isExpanded = true;

    /// <summary>カテゴリ名。UIのグループヘッダーとして表示される。</summary>
    public string Name { get; }

    /// <summary>全オプション項目（フィルタのソース。UI からは参照しない）</summary>
    private readonly IReadOnlyList<OptionItemViewModel> _allItems;

    /// <summary>全アクション項目（フィルタのソース。UI からは参照しない）</summary>
    private readonly IReadOnlyList<ActionItemViewModel> _allActions;

    /// <summary>フィルタ適用後のオプション項目一覧のバッキングフィールド</summary>
    private ObservableCollection<OptionItemViewModel> _filteredItems = new();

    /// <summary>フィルタ適用後のアクション項目一覧のバッキングフィールド</summary>
    private ObservableCollection<ActionItemViewModel> _filteredActions = new();

    /// <summary>
    /// フィルタ適用後のオプション項目一覧（UIバインド用）。
    /// ApplyFilter 時はコレクション丸ごと差し替えで単一 PropertyChanged 通知に最適化。
    /// </summary>
    public ObservableCollection<OptionItemViewModel> FilteredItems
    {
        get => _filteredItems;
        private set => SetProperty(ref _filteredItems, value);
    }

    /// <summary>
    /// フィルタ適用後のアクション項目一覧（UIバインド用）。
    /// ApplyFilter 時はコレクション丸ごと差し替えで単一 PropertyChanged 通知に最適化。
    /// </summary>
    public ObservableCollection<ActionItemViewModel> FilteredActions
    {
        get => _filteredActions;
        private set => SetProperty(ref _filteredActions, value);
    }

    /// <summary>カテゴリの展開/折りたたみ状態</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>展開/折りたたみをトグルするコマンド</summary>
    public ICommand ToggleExpandCommand { get; }

    public OptionCategoryViewModel(OptionCategory category)
    {
        Name = category.Name;
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);

        var items = new List<OptionItemViewModel>(category.Options.Count);
        foreach (var opt in category.Options)
            items.Add(new OptionItemViewModel(opt));
        _allItems = items;

        var actions = new List<ActionItemViewModel>(category.Actions.Count);
        foreach (var act in category.Actions)
            actions.Add(new ActionItemViewModel(act));
        _allActions = actions;

        ApplyFilter(null);
    }

    /// <summary>
    /// 検索クエリに基づいてフィルタリングを適用する。
    /// null で全アイテム表示。大文字小文字を区別しない部分一致検索。
    /// オプションは表示名・説明文の両方を検索対象にする（アクション側との対称性を確保）。
    /// </summary>
    public void ApplyFilter(string? query)
    {
        // 空クエリ → 全件をコレクション丸ごと差し替え
        if (string.IsNullOrEmpty(query))
        {
            FilteredItems = new ObservableCollection<OptionItemViewModel>(_allItems);
            FilteredActions = new ObservableCollection<ActionItemViewModel>(_allActions);
            return;
        }

        // クエリヒットしたアイテム/アクションを一時リストに構築してから丸ごと差し替え
        var matchedItems = new List<OptionItemViewModel>();
        foreach (var item in _allItems)
        {
            // 表示名・説明文のいずれかにヒットしたら採用（アクション側と同じ非対称解消）
            if (item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (item.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                matchedItems.Add(item);
            }
        }

        var matchedActions = new List<ActionItemViewModel>();
        foreach (var action in _allActions)
        {
            if (action.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (action.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                matchedActions.Add(action);
            }
        }

        FilteredItems = new ObservableCollection<OptionItemViewModel>(matchedItems);
        FilteredActions = new ObservableCollection<ActionItemViewModel>(matchedActions);
    }
}

/// <summary>
/// 個別オプション項目のViewModel。
/// <see cref="OptionDescriptor"/> をラップし、値の取得・設定・型変換を担う。
/// </summary>
public class OptionItemViewModel : ViewModelBase
{
    private readonly OptionDescriptor _descriptor;

    public string DisplayName => _descriptor.DisplayName;
    public OptionKind Kind => _descriptor.Kind;
    public bool IsReadOnly => _descriptor.IsReadOnly;
    public double? Min => _descriptor.Range?.Min;
    public double? Max => _descriptor.Range?.Max;
    public double? Step => _descriptor.Range?.Step;
    public string[]? EnumNames => _descriptor.EnumNames;

    /// <summary>説明テキスト（null なら UI 非表示。NotNullOrEmptyConverter で判定する）</summary>
    public string? Description => _descriptor.Description;

    /// <summary>
    /// オプションの現在値。setter は型変換後に書き戻す。
    /// 型変換に失敗した場合（FormatException 等）は値を変更せず、
    /// UI バインディングを旧値に戻すため OnPropertyChanged を発火する。
    /// </summary>
    public object? Value
    {
        get => _descriptor.Getter();
        set
        {
            if (_descriptor.Setter == null) return;

            try
            {
                _descriptor.Setter(ConvertValue(value));
                OnPropertyChanged();
            }
            catch (Exception ex) when (
                ex is FormatException ||
                ex is OverflowException ||
                ex is InvalidCastException ||
                ex is ArgumentException)
            {
                // 不正な入力（"abc" → int など）を握りつぶし、UI を旧値表示に戻す
                // OnPropertyChanged の発火で TextBox 等が getter から最新値を取り直す
                OnPropertyChanged();
            }
        }
    }

    public OptionItemViewModel(OptionDescriptor descriptor)
    {
        _descriptor = descriptor;
    }

    /// <summary>
    /// UI入力値をターゲット型に変換する。
    /// enum は文字列からパース、その他は Convert.ChangeType でキャスト。
    /// </summary>
    private object? ConvertValue(object? value)
    {
        if (value == null) return null;
        var targetType = _descriptor.ValueType;

        // ComboBox の SelectedItem が string のため enum 名からパースが必要
        if (targetType.IsEnum && value is string s)
            return Enum.Parse(targetType, s);

        return Convert.ChangeType(value, targetType);
    }
}

/// <summary>
/// ボタンアクション項目のViewModel。
/// 非同期実行のステータス管理（スピナー・成功/失敗フィードバック）を含む。
/// <see cref="OptionItemViewModel"/> とは独立したクラス（継承関係なし）。
/// </summary>
public sealed class ActionItemViewModel : ViewModelBase
{
    private readonly ActionDescriptor _action;
    private ActionStatus _status = ActionStatus.Idle;

    public string Label => _action.Label;

    /// <summary>説明テキスト（null なら UI 非表示。NotNullOrEmptyConverter で判定する）</summary>
    public string? Description => _action.Description;

    /// <summary>
    /// 現在の実行状態。変更時に IsRunning も連動通知する。
    /// Idle → Running → Success/Failed → 2秒後 Idle。
    /// </summary>
    public ActionStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(IsRunning));
        }
    }

    /// <summary>実行中かどうか（ボタン無効化とスピナー表示に使用）</summary>
    public bool IsRunning => Status == ActionStatus.Running;

    public ICommand ExecuteCommand { get; }

    public ActionItemViewModel(ActionDescriptor action)
    {
        _action = action;
        ExecuteCommand = new RelayCommand(OnExecute);
    }

    /// <summary>
    /// アクションを非同期実行し、ステータスを管理する。
    /// async void は ICommand.Execute が void を返すため使用。
    /// try/catch で例外を確実に捕捉するため安全。
    /// </summary>
    private async void OnExecute()
    {
        if (Status == ActionStatus.Running) return;

        Status = ActionStatus.Running;
        try
        {
            await _action.ExecuteAsync();
            Status = ActionStatus.Success;
        }
        catch
        {
            Status = ActionStatus.Failed;
        }

        await Task.Delay(2000);
        if (Status != ActionStatus.Running)
            Status = ActionStatus.Idle;
    }
}
