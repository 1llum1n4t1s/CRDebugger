using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Options;

/// <summary>
/// オプションプロパティの UI コントロール種別を表す列挙型。
/// <see cref="OptionsEngine"/> がプロパティの型を解析して割り当て、
/// UI 側がこの値を元に適切なコントロール（チェックボックス、スライダー等）を選択する。
/// </summary>
public enum OptionKind
{
    /// <summary>チェックボックス（bool 型プロパティ用）</summary>
    Boolean,
    /// <summary>整数入力またはスライダー（int / long / short / byte 等の整数型用）</summary>
    Integer,
    /// <summary>小数入力またはスライダー（float / double / decimal 型用）</summary>
    Float,
    /// <summary>テキスト入力ボックス（string 型用）</summary>
    String,
    /// <summary>ドロップダウン選択リスト（enum 型用）</summary>
    Enum,
    /// <summary>読み取り専用の値表示（セッターなし、またはサポート外の型用）</summary>
    ReadOnly,
    /// <summary>カラーピッカー（#RRGGBB 形式の string 型用）</summary>
    Color
}

/// <summary>
/// リフレクションまたは <see cref="DynamicOptionContainer"/> によって解析された
/// オプションプロパティの記述子。
/// UI にバインドされる表示名・カテゴリ・種別・値の getter/setter を保持する。
/// </summary>
public sealed class OptionDescriptor
{
    /// <summary>オプションの一意識別子（"型名.プロパティ名" 形式）</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>UI に表示するラベル名</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>所属カテゴリ名（グループ化に使用される。デフォルトは "General"）</summary>
    public string Category { get; init; } = "General";

    /// <summary>カテゴリ内の表示順（昇順、デフォルトは 0）</summary>
    public int SortOrder { get; init; }

    /// <summary>UI コントロールの種類（<see cref="OptionKind"/> を参照）</summary>
    public OptionKind Kind { get; init; }

    /// <summary>プロパティの実際の型（セッターでのキャストに使用）</summary>
    public Type ValueType { get; init; } = typeof(object);

    /// <summary>現在値を <c>object?</c> として取得するコンパイル済みデリゲート</summary>
    public Func<object?> Getter { get; init; } = () => null;

    /// <summary>
    /// 値を <c>object?</c> で受け取り設定するコンパイル済みデリゲート。
    /// 読み取り専用プロパティの場合は <c>null</c>。
    /// </summary>
    public Action<object?>? Setter { get; init; }

    /// <summary>セッターが <c>null</c> の場合は読み取り専用と判断する</summary>
    public bool IsReadOnly => Setter == null;

    /// <summary>
    /// 数値プロパティのスライダー範囲制約（min / max / step）。
    /// 設定されていない場合は <c>null</c>（テキスト入力として表示される）。
    /// </summary>
    public CRRangeAttribute? Range { get; init; }

    /// <summary>
    /// enum 型プロパティの選択肢の名前一覧（ドロップダウン用）。
    /// enum 型以外では <c>null</c>。
    /// </summary>
    public string[]? EnumNames { get; init; }

    /// <summary>
    /// オプションの説明テキスト。<see cref="CRDescriptionAttribute"/> から取得。
    /// <c>null</c> の場合は UI に説明を表示しない。
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// <see cref="CRActionAttribute"/> が付いたメソッドから生成されるボタンアクションの記述子。
/// UI ではボタンとして表示され、押下時に <see cref="Execute"/> が呼び出される。
/// </summary>
public sealed class ActionDescriptor
{
    /// <summary>アクションの一意識別子（"型名.メソッド名" 形式）</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>ボタンに表示するラベル</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>所属カテゴリ名（グループ化に使用される。デフォルトは "General"）</summary>
    public string Category { get; init; } = "General";

    /// <summary>カテゴリ内の表示順（昇順、デフォルトは 0）</summary>
    public int SortOrder { get; init; }

    /// <summary>ボタン押下時に実行するデリゲート（同期）</summary>
    public Action Execute { get; init; } = () => { };

    /// <summary>
    /// ボタン押下時に実行する非同期デリゲート。
    /// 同期メソッドの場合は Task.CompletedTask を返すラッパーが設定される。
    /// </summary>
    public Func<Task> ExecuteAsync { get; init; } = () => Task.CompletedTask;

    /// <summary>
    /// アクションの説明テキスト。<see cref="CRDescriptionAttribute"/> から取得。
    /// <c>null</c> の場合は UI に説明を表示しない。
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// カテゴリ名でグループ化されたオプション群とアクション群のコンテナ。
/// <see cref="OptionsEngine.ScanAll"/> が返す結果の単位となり、
/// UI ではカテゴリごとにセクション分けして表示される。
/// </summary>
public sealed class OptionCategory
{
    /// <summary>カテゴリ名（UI のセクションヘッダーとして表示される）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>このカテゴリに属するオプション記述子の一覧（<see cref="OptionDescriptor.SortOrder"/> 昇順）</summary>
    public IReadOnlyList<OptionDescriptor> Options { get; init; } = [];

    /// <summary>このカテゴリに属するアクション記述子の一覧（<see cref="ActionDescriptor.SortOrder"/> 昇順）</summary>
    public IReadOnlyList<ActionDescriptor> Actions { get; init; } = [];
}
