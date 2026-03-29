using System.Collections.ObjectModel;
using System.Windows.Input;
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

    /// <summary>カテゴリの展開/折りたたみ状態をリフレッシュ間で保持する辞書</summary>
    private readonly Dictionary<string, bool> _expandedState = new();

    private string _searchText = string.Empty;

    /// <summary>フィルタ前の完全なカテゴリ一覧（ApplyFilter のソース）</summary>
    private readonly List<OptionCategoryViewModel> _allCategories = new();

    /// <summary>前回の ApplyFilter で使用したクエリ（同値ガード用）</summary>
    private string _lastAppliedQuery = string.Empty;

    /// <summary>フィルタ適用後のカテゴリ一覧。UIのリストに直接バインドされる。</summary>
    public ObservableCollection<OptionCategoryViewModel> FilteredCategories { get; } = new();

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

    public OptionsViewModel(OptionsEngine engine)
    {
        _engine = engine;
        RefreshCommand = new RelayCommand(Refresh);
        _engine.ContainersChanged += (_, _) => Refresh();
        Refresh();
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

        FilteredCategories.Clear();

        foreach (var cat in _allCategories)
        {
            if (string.IsNullOrEmpty(query))
            {
                cat.ApplyFilter(null);
                FilteredCategories.Add(cat);
                continue;
            }

            // カテゴリ名ヒットでカテゴリ丸ごと表示
            if (cat.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                cat.ApplyFilter(null);
                FilteredCategories.Add(cat);
                continue;
            }

            // アイテム/アクション単位でフィルタ
            cat.ApplyFilter(query);
            if (cat.FilteredItems.Count > 0 || cat.FilteredActions.Count > 0)
                FilteredCategories.Add(cat);
        }
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

    /// <summary>フィルタ適用後のオプション項目一覧（UIバインド用）</summary>
    public ObservableCollection<OptionItemViewModel> FilteredItems { get; } = new();

    /// <summary>フィルタ適用後のアクション項目一覧（UIバインド用）</summary>
    public ObservableCollection<ActionItemViewModel> FilteredActions { get; } = new();

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
    /// </summary>
    public void ApplyFilter(string? query)
    {
        FilteredItems.Clear();
        FilteredActions.Clear();

        if (string.IsNullOrEmpty(query))
        {
            foreach (var item in _allItems) FilteredItems.Add(item);
            foreach (var action in _allActions) FilteredActions.Add(action);
            return;
        }

        foreach (var item in _allItems)
        {
            if (item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                FilteredItems.Add(item);
        }

        foreach (var action in _allActions)
        {
            if (action.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (action.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                FilteredActions.Add(action);
        }
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
    /// </summary>
    public object? Value
    {
        get => _descriptor.Getter();
        set
        {
            if (_descriptor.Setter != null)
            {
                _descriptor.Setter(ConvertValue(value));
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
