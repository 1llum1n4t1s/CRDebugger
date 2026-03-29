using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Options;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// オプション設定画面のViewModel。
/// <see cref="OptionsEngine"/> が管理する全コンテナをスキャンし、
/// カテゴリ別にグループ化されたオプション項目を提供する。
/// 検索フィルタリングとカテゴリ折りたたみ機能を含む。
/// コンテナの変更イベントを購読し、自動的に再スキャンを行う。
/// </summary>
public sealed class OptionsViewModel : ViewModelBase
{
    /// <summary>オプションコンテナのスキャンを担うエンジン</summary>
    private readonly OptionsEngine _engine;

    /// <summary>
    /// カテゴリの展開/折りたたみ状態をリフレッシュ間で保持するための辞書。
    /// キーはカテゴリ名、値は展開状態（true=展開、false=折りたたみ）。
    /// <see cref="Refresh"/> 後も前回の展開状態を復元するために使用する。
    /// </summary>
    private readonly Dictionary<string, bool> _expandedState = new();

    /// <summary>検索テキストのバッキングフィールド</summary>
    private string _searchText = string.Empty;

    /// <summary>
    /// 全カテゴリ一覧（フィルタ前の完全なリスト）。
    /// <see cref="ApplyFilter"/> のソースとして使用し、
    /// 検索テキストに応じて <see cref="FilteredCategories"/> を再構築する。
    /// </summary>
    private readonly List<OptionCategoryViewModel> _allCategories = new();

    /// <summary>
    /// フィルタ適用後のカテゴリ一覧。
    /// UIのリストに直接バインドされる。検索テキストが空の場合は全カテゴリを含む。
    /// </summary>
    public ObservableCollection<OptionCategoryViewModel> FilteredCategories { get; } = new();

    /// <summary>
    /// 検索テキスト。変更時に自動でフィルタリングを実行する。
    /// カテゴリ名、オプション表示名、アクションラベル、説明テキストが検索対象となる。
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set { SetProperty(ref _searchText, value); ApplyFilter(); }
    }

    /// <summary>
    /// オプション一覧を手動で再スキャンするコマンド。
    /// コンテナの登録/解除後にUIを更新したい場合に使用する。
    /// </summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// <see cref="OptionsViewModel"/> のインスタンスを生成する。
    /// </summary>
    /// <param name="engine">オプションコンテナのスキャンを担うエンジン</param>
    public OptionsViewModel(OptionsEngine engine)
    {
        _engine = engine;
        RefreshCommand = new RelayCommand(Refresh);
        // コンテナの登録/解除イベントを購読して自動再スキャン
        _engine.ContainersChanged += (_, _) => Refresh();
        // 初期スキャンを実行してオプション一覧を構築
        Refresh();
    }

    /// <summary>
    /// エンジンを通じてコンテナを再スキャンし、カテゴリ一覧を更新する。
    /// 展開状態は <see cref="_expandedState"/> に保持され、リフレッシュ後も復元される。
    /// </summary>
    public void Refresh()
    {
        // 現在の展開状態を保存（リフレッシュ後に復元するため）
        foreach (var cat in _allCategories)
            _expandedState[cat.Name] = cat.IsExpanded;

        // 全カテゴリをクリアして再構築
        _allCategories.Clear();
        var cats = _engine.ScanAll();
        foreach (var cat in cats)
        {
            var vm = new OptionCategoryViewModel(cat);
            // 展開状態を復元（初回登録時はデフォルトの展開状態を維持）
            if (_expandedState.TryGetValue(vm.Name, out var expanded))
                vm.IsExpanded = expanded;
            _allCategories.Add(vm);
        }

        // 検索フィルタを再適用して FilteredCategories を更新
        ApplyFilter();
    }

    /// <summary>
    /// 検索テキストに基づいてフィルタリングを適用し、<see cref="FilteredCategories"/> を更新する。
    /// フィルタロジック:
    /// 1. 検索テキストが空 → 全カテゴリ・全アイテムを表示
    /// 2. カテゴリ名がヒット → そのカテゴリの全アイテムを表示
    /// 3. アイテム/アクション名がヒット → ヒットしたアイテムのみ表示
    /// </summary>
    private void ApplyFilter()
    {
        FilteredCategories.Clear();
        var query = _searchText.Trim();

        foreach (var cat in _allCategories)
        {
            if (string.IsNullOrEmpty(query))
            {
                // 検索テキストが空 → フィルタなしで全カテゴリを表示
                cat.ApplyFilter(null);
                FilteredCategories.Add(cat);
                continue;
            }

            // カテゴリ名が検索テキストを含む → カテゴリ丸ごと表示（アイテムはフィルタしない）
            if (cat.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                cat.ApplyFilter(null);
                FilteredCategories.Add(cat);
                continue;
            }

            // カテゴリ名にヒットしない → アイテム/アクション単位でフィルタ
            cat.ApplyFilter(query);
            // 1件でもヒットしたアイテムまたはアクションがあればカテゴリを表示
            if (cat.FilteredItems.Count > 0 || cat.FilteredActions.Count > 0)
                FilteredCategories.Add(cat);
        }
    }
}

/// <summary>
/// オプションカテゴリのViewModel。
/// 同一カテゴリに属するオプション項目とアクション項目をまとめて保持する。
/// 折りたたみ機能と検索フィルタリング機能を含む。
/// </summary>
public sealed class OptionCategoryViewModel : ViewModelBase
{
    /// <summary>折りたたみ状態のバッキングフィールド（デフォルト: 展開）</summary>
    private bool _isExpanded = true;

    /// <summary>カテゴリ名。UIのグループヘッダーとして表示される。</summary>
    public string Name { get; }

    /// <summary>
    /// このカテゴリに属するオプション項目の完全一覧。
    /// 検索フィルタの有無に関わらず全アイテムを保持する（フィルタのソース）。
    /// </summary>
    public ObservableCollection<OptionItemViewModel> Items { get; } = new();

    /// <summary>
    /// このカテゴリに属するアクション項目の完全一覧。
    /// 検索フィルタの有無に関わらず全アクションを保持する（フィルタのソース）。
    /// </summary>
    public ObservableCollection<ActionItemViewModel> Actions { get; } = new();

    /// <summary>
    /// フィルタ適用後のオプション項目一覧。
    /// UIにバインドされ、検索テキストに一致するアイテムのみ含む。
    /// </summary>
    public ObservableCollection<OptionItemViewModel> FilteredItems { get; } = new();

    /// <summary>
    /// フィルタ適用後のアクション項目一覧。
    /// UIにバインドされ、検索テキストに一致するアクションのみ含む。
    /// </summary>
    public ObservableCollection<ActionItemViewModel> FilteredActions { get; } = new();

    /// <summary>
    /// カテゴリの展開/折りたたみ状態。
    /// <c>true</c> で展開（アイテム表示）、<c>false</c> で折りたたみ（ヘッダーのみ表示）。
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>展開/折りたたみをトグルするコマンド。カテゴリヘッダーのクリックにバインドされる。</summary>
    public ICommand ToggleExpandCommand { get; }

    /// <summary>
    /// <see cref="OptionCategoryViewModel"/> のインスタンスを生成する。
    /// </summary>
    /// <param name="category">元となるオプションカテゴリデータ</param>
    public OptionCategoryViewModel(OptionCategory category)
    {
        Name = category.Name;
        // ヘッダークリックで IsExpanded を反転するトグルコマンド
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);

        // オプション項目を OptionDescriptor → OptionItemViewModel に変換
        foreach (var opt in category.Options)
            Items.Add(new OptionItemViewModel(opt));
        // アクション項目を ActionDescriptor → ActionItemViewModel に変換
        foreach (var act in category.Actions)
            Actions.Add(new ActionItemViewModel(act));

        // 初期状態ではフィルタなし（全アイテム表示）
        ApplyFilter(null);
    }

    /// <summary>
    /// 検索クエリに基づいてフィルタリングを適用し、
    /// <see cref="FilteredItems"/> と <see cref="FilteredActions"/> を再構築する。
    /// </summary>
    /// <param name="query">
    /// 検索クエリ文字列。<c>null</c> または空文字列の場合は全アイテムを表示する。
    /// 大文字小文字を区別しない部分一致検索を行う。
    /// </param>
    public void ApplyFilter(string? query)
    {
        // 既存のフィルタ結果をクリアして再構築
        FilteredItems.Clear();
        FilteredActions.Clear();

        if (string.IsNullOrEmpty(query))
        {
            // フィルタなし → 全アイテム・全アクションを表示
            foreach (var item in Items) FilteredItems.Add(item);
            foreach (var action in Actions) FilteredActions.Add(action);
            return;
        }

        // オプション項目を表示名で部分一致検索
        foreach (var item in Items)
        {
            if (item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                FilteredItems.Add(item);
        }

        // アクション項目をラベルと説明テキストで部分一致検索
        foreach (var action in Actions)
        {
            // ラベルまたは説明テキストのいずれかにヒットすれば表示する
            if (action.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (action.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                FilteredActions.Add(action);
        }
    }
}

/// <summary>
/// 個別オプション項目のViewModel。
/// <see cref="OptionDescriptor"/> をラップし、値の取得・設定・型変換を担う。
/// Boolean / Integer / Float / String / Enum / Color / ReadOnly の各 Kind に対応する。
/// </summary>
public class OptionItemViewModel : ViewModelBase
{
    /// <summary>オプションのメタデータ（表示名・種類・バリデーション情報等）を保持する記述子</summary>
    private readonly OptionDescriptor _descriptor;

    /// <summary>UI上の表示名。<see cref="OptionDescriptor.DisplayName"/> から取得する。</summary>
    public string DisplayName => _descriptor.DisplayName;

    /// <summary>
    /// オプションの種類（bool / int / float / enum / string / Color / ReadOnly）。
    /// UI側でこの値に応じて表示コントロールを切り替える。
    /// </summary>
    public OptionKind Kind => _descriptor.Kind;

    /// <summary>
    /// 読み取り専用かどうか。<c>true</c> の場合はUIの入力コントロールを無効化する。
    /// セッターが <c>null</c> のプロパティは自動的に読み取り専用になる。
    /// </summary>
    public bool IsReadOnly => _descriptor.IsReadOnly;

    /// <summary>数値型オプションの最小値。範囲制約がない場合は <c>null</c>。</summary>
    public double? Min => _descriptor.Range?.Min;

    /// <summary>数値型オプションの最大値。範囲制約がない場合は <c>null</c>。</summary>
    public double? Max => _descriptor.Range?.Max;

    /// <summary>スライダーのステップ値（1目盛りの増減量）。範囲制約がない場合は <c>null</c>。</summary>
    public double? Step => _descriptor.Range?.Step;

    /// <summary>enum型オプションの選択肢名一覧（ドロップダウン用）。enum以外の型では <c>null</c>。</summary>
    public string[]? EnumNames => _descriptor.EnumNames;

    /// <summary>
    /// オプションの説明テキスト。<see cref="Options.Attributes.CRDescriptionAttribute"/> から取得。
    /// <c>null</c> の場合は UI に説明を表示しない。
    /// </summary>
    public string? Description => _descriptor.Description;

    /// <summary>
    /// 説明テキストが存在するかどうか。
    /// Avalonia の IsVisible バインディングで使用する（compiled binding で null チェックを避けるため）。
    /// </summary>
    public bool HasDescription => _descriptor.Description != null;

    /// <summary>
    /// オプションの現在値。
    /// getter は <see cref="OptionDescriptor.Getter"/> を呼び出して実際の値を取得し、
    /// setter は型変換後に <see cref="OptionDescriptor.Setter"/> 経由で書き戻す。
    /// </summary>
    public object? Value
    {
        get => _descriptor.Getter();
        set
        {
            if (_descriptor.Setter != null)
            {
                // 入力値をターゲット型に変換してからセッターで書き込む
                _descriptor.Setter(ConvertValue(value));
                // 変更をUIへ通知（PropertyChanged を発火）
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// <see cref="OptionItemViewModel"/> のインスタンスを生成する。
    /// </summary>
    /// <param name="descriptor">オプションのメタデータを保持する記述子</param>
    public OptionItemViewModel(OptionDescriptor descriptor)
    {
        _descriptor = descriptor;
    }

    /// <summary>
    /// UI入力値をオプションのターゲット型に変換するヘルパー。
    /// enum型かつ文字列入力の場合は <see cref="Enum.Parse(Type, string)"/> を使用し、
    /// それ以外は <see cref="Convert.ChangeType(object, Type)"/> でキャストする。
    /// </summary>
    /// <param name="value">変換前の値（UI入力値）</param>
    /// <returns>ターゲット型に変換された値</returns>
    private object? ConvertValue(object? value)
    {
        // null は変換せずそのまま返す
        if (value == null) return null;
        var targetType = _descriptor.ValueType;

        // enum型かつ文字列入力の場合は名前から列挙値にパース（ComboBox の SelectedItem が string のため）
        if (targetType.IsEnum && value is string s)
            return Enum.Parse(targetType, s);

        // その他は汎用型変換を使用（NumericUpDown の decimal → int 等）
        return Convert.ChangeType(value, targetType);
    }
}

/// <summary>
/// ボタンアクション項目のViewModel。
/// <see cref="ActionDescriptor"/> をラップし、非同期実行のステータス管理を担う。
/// 実行中はスピナー表示、完了後に成功(✓)/失敗(×)のフィードバックアイコンを 2 秒間表示する。
/// <see cref="OptionItemViewModel"/> とは独立したクラス（継承関係なし）。
/// </summary>
public sealed class ActionItemViewModel : ViewModelBase
{
    /// <summary>アクションの記述子。ラベル・実行デリゲート・説明テキストを保持する。</summary>
    private readonly ActionDescriptor _action;

    /// <summary>現在の実行状態のバッキングフィールド</summary>
    private ActionStatus _status = ActionStatus.Idle;

    /// <summary>UIのボタン上に表示されるラベルテキスト</summary>
    public string Label => _action.Label;

    /// <summary>
    /// アクションの説明テキスト。<see cref="Options.Attributes.CRDescriptionAttribute"/> から取得。
    /// <c>null</c> の場合は UI に説明を表示しない。
    /// </summary>
    public string? Description => _action.Description;

    /// <summary>
    /// 説明テキストが存在するかどうか。
    /// Avalonia の IsVisible バインディングで使用する。
    /// </summary>
    public bool HasDescription => _action.Description != null;

    /// <summary>
    /// 現在の実行状態。
    /// <see cref="ActionStatus.Idle"/> → <see cref="ActionStatus.Running"/> →
    /// <see cref="ActionStatus.Success"/> or <see cref="ActionStatus.Failed"/> →
    /// 2秒後に <see cref="ActionStatus.Idle"/> に戻る。
    /// </summary>
    public ActionStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                // IsRunning も連動して変更通知を発火（ボタンの IsEnabled バインディング用）
                OnPropertyChanged(nameof(IsRunning));
            }
        }
    }

    /// <summary>
    /// アクションが実行中かどうか。
    /// <c>true</c> の場合はボタンを無効化してスピナーを表示する。
    /// </summary>
    public bool IsRunning => Status == ActionStatus.Running;

    /// <summary>ボタン押下時に実行するコマンド。<see cref="OnExecute"/> をラップする。</summary>
    public ICommand ExecuteCommand { get; }

    /// <summary>
    /// <see cref="ActionItemViewModel"/> のインスタンスを生成する。
    /// </summary>
    /// <param name="action">アクション記述子（ラベル・実行デリゲート・説明テキストを含む）</param>
    public ActionItemViewModel(ActionDescriptor action)
    {
        _action = action;
        // RelayCommand で非同期実行メソッドをラップ（async void パターン）
        ExecuteCommand = new RelayCommand(OnExecute);
    }

    /// <summary>
    /// アクションを非同期で実行し、ステータスを管理する。
    /// ICommand.Execute は void を返すため async void を使用するが、
    /// try/catch で例外を確実に捕捉するため安全に動作する。
    /// </summary>
    private async void OnExecute()
    {
        // 実行中は再実行を防止（連打対策）
        if (Status == ActionStatus.Running) return;

        // ステータスを Running に変更 → UI がスピナーを表示しボタンを無効化
        Status = ActionStatus.Running;
        try
        {
            // ActionDescriptor.ExecuteAsync を await する
            // （同期メソッドの場合は Task.CompletedTask が即座に返る）
            await _action.ExecuteAsync();
            // 正常完了 → 成功アイコン（緑✓）を表示
            Status = ActionStatus.Success;
        }
        catch
        {
            // 例外発生 → 失敗アイコン（赤×）を表示
            Status = ActionStatus.Failed;
        }

        // 2秒間フィードバックアイコンを表示した後、通常状態に戻す
        await Task.Delay(2000);
        // 2秒待機中に新たな実行が始まっていなければ Idle に戻す
        if (Status != ActionStatus.Running)
            Status = ActionStatus.Idle;
    }
}
