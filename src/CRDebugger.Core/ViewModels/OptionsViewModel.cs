using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CRDebugger.Core.Options;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// オプション設定画面のViewModel。
/// <see cref="OptionsEngine"/> が管理する全コンテナをスキャンし、
/// カテゴリ別にグループ化されたオプション項目を提供する。
/// コンテナの変更イベントを購読し、自動的に再スキャンを行う。
/// </summary>
public sealed class OptionsViewModel : ViewModelBase
{
    /// <summary>オプションコンテナのスキャンを担うエンジン</summary>
    private readonly OptionsEngine _engine;

    /// <summary>
    /// カテゴリ別のオプション一覧。
    /// UIのリストに直接バインドされる。
    /// </summary>
    public ObservableCollection<OptionCategoryViewModel> Categories { get; } = new();

    /// <summary>
    /// オプション一覧を手動で再スキャンするコマンド。
    /// コンテナの登録/解除後にUIを更新したい場合に使用する。
    /// </summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// <see cref="OptionsViewModel"/> のインスタンスを生成する
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
    /// エンジンを通じてコンテナを再スキャンし、<see cref="Categories"/> を更新する。
    /// スキャン結果をカテゴリ別にグループ化して表示用ViewModelに変換する。
    /// </summary>
    public void Refresh()
    {
        // 既存のカテゴリ一覧をクリアして再構築
        Categories.Clear();
        var cats = _engine.ScanAll();
        foreach (var cat in cats)
        {
            // OptionCategoryをViewModelでラップしてコレクションに追加
            Categories.Add(new OptionCategoryViewModel(cat));
        }
    }
}

/// <summary>
/// オプションカテゴリのViewModel。
/// 同一カテゴリに属するオプション項目とアクション項目をまとめて保持する。
/// </summary>
public sealed class OptionCategoryViewModel : ViewModelBase
{
    /// <summary>カテゴリ名。UIのグループヘッダーとして表示される。</summary>
    public string Name { get; }

    /// <summary>
    /// このカテゴリに属するオプション項目（bool/int/float/enum/string 等）の一覧
    /// </summary>
    public ObservableCollection<OptionItemViewModel> Items { get; } = new();

    /// <summary>
    /// このカテゴリに属するボタンアクション項目の一覧
    /// </summary>
    public ObservableCollection<ActionItemViewModel> Actions { get; } = new();

    /// <summary>
    /// <see cref="OptionCategoryViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="category">元となるオプションカテゴリデータ</param>
    public OptionCategoryViewModel(OptionCategory category)
    {
        Name = category.Name;
        // オプション項目をViewModelに変換して追加
        foreach (var opt in category.Options)
            Items.Add(new OptionItemViewModel(opt));
        // アクション項目をViewModelに変換して追加
        foreach (var act in category.Actions)
            Actions.Add(new ActionItemViewModel(act));
    }
}

/// <summary>
/// 個別オプション項目のViewModel。
/// <see cref="OptionDescriptor"/> をラップし、値の取得・設定・型変換を担う。
/// </summary>
public class OptionItemViewModel : ViewModelBase
{
    /// <summary>オプションのメタデータ（表示名・種類・バリデーション情報等）を保持する記述子</summary>
    private readonly OptionDescriptor _descriptor;

    /// <summary>
    /// UI上の表示名。<see cref="OptionDescriptor.DisplayName"/> から取得する。
    /// </summary>
    public string DisplayName => _descriptor.DisplayName;

    /// <summary>
    /// オプションの種類（bool / int / float / enum / string / ReadOnly 等）
    /// </summary>
    public OptionKind Kind => _descriptor.Kind;

    /// <summary>
    /// 読み取り専用かどうか。<c>true</c> の場合はUIの入力コントロールを無効化する。
    /// </summary>
    public bool IsReadOnly => _descriptor.IsReadOnly;

    /// <summary>
    /// 数値型オプションの最小値。範囲制約がない場合は <c>null</c>。
    /// </summary>
    public double? Min => _descriptor.Range?.Min;

    /// <summary>
    /// 数値型オプションの最大値。範囲制約がない場合は <c>null</c>。
    /// </summary>
    public double? Max => _descriptor.Range?.Max;

    /// <summary>
    /// スライダーのステップ値。範囲制約がない場合は <c>null</c>。
    /// </summary>
    public double? Step => _descriptor.Range?.Step;

    /// <summary>
    /// enum型オプションの選択肢名一覧。enum以外の型では <c>null</c>。
    /// </summary>
    public string[]? EnumNames => _descriptor.EnumNames;

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
                // 変更をUIへ通知
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// <see cref="OptionItemViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="descriptor">オプションのメタデータを保持する記述子</param>
    public OptionItemViewModel(OptionDescriptor descriptor)
    {
        _descriptor = descriptor;

        // INotifyPropertyChangedを実装したコンテナの変更を監視
        // (OptionDescriptorのGetterが参照するインスタンスから)
    }

    /// <summary>
    /// UI入力値をオプションのターゲット型に変換するヘルパー。
    /// enum型かつ文字列入力の場合は Enum.Parse を使用し、
    /// それ以外は Convert.ChangeType でキャストする。
    /// </summary>
    /// <param name="value">変換前の値（UI入力値）</param>
    /// <returns>ターゲット型に変換された値</returns>
    private object? ConvertValue(object? value)
    {
        // null は変換せずそのまま返す
        if (value == null) return null;
        var targetType = _descriptor.ValueType;

        // enum型かつ文字列入力の場合は名前から列挙値にパース（Enum.Parse を使用）
        if (targetType.IsEnum && value is string s)
            return Enum.Parse(targetType, s);

        // その他は汎用型変換を使用
        return Convert.ChangeType(value, targetType);
    }
}

/// <summary>
/// ボタンアクション項目のViewModel。
/// オプション画面上に「ボタン」として表示される操作を表す。
/// <see cref="OptionItemViewModel"/> を継承し、ラベルと実行コマンドを追加する。
/// </summary>
public sealed class ActionItemViewModel : OptionItemViewModel
{
    /// <summary>アクションの記述子。ボタンラベルと実行処理を保持する。</summary>
    private readonly ActionDescriptor _action;

    /// <summary>
    /// UIのボタン上に表示されるラベルテキスト
    /// </summary>
    public string Label => _action.Label;

    /// <summary>
    /// ボタン押下時に実行するコマンド。
    /// <see cref="ActionDescriptor.Execute"/> をRelayCommandでラップしている。
    /// </summary>
    public ICommand ExecuteCommand { get; }

    /// <summary>
    /// <see cref="ActionItemViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="action">ボタンのラベルと実行処理を保持するアクション記述子</param>
    public ActionItemViewModel(ActionDescriptor action)
        : base(new OptionDescriptor
        {
            // アクションはオプション値を持たないためReadOnlyとして登録
            Id = action.Id,
            DisplayName = action.Label,
            Category = action.Category,
            Kind = OptionKind.ReadOnly,
            ValueType = typeof(void)
        })
    {
        _action = action;
        // アクションの実行デリゲートをコマンドとして公開
        ExecuteCommand = new RelayCommand(action.Execute);
    }
}
