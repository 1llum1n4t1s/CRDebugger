using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Options;

/// <summary>
/// コードから直接オプションを動的に定義するコンテナ。
/// リフレクションを使わずに、メソッドチェーンで型安全にオプションを登録できる。
/// 登録後は <see cref="OptionsEngine"/> に渡して UI に表示する。
/// </summary>
public sealed class DynamicOptionContainer
{
    /// <summary>動的に追加されたオプション記述子の内部リスト</summary>
    private readonly List<OptionDescriptor> _options = new();

    /// <summary>動的に追加されたアクション記述子の内部リスト</summary>
    private readonly List<ActionDescriptor> _actions = new();

    /// <summary>このコンテナに属するオプション・アクションのカテゴリ名</summary>
    private readonly string _category;

    /// <summary>
    /// <see cref="DynamicOptionContainer"/> のインスタンスを生成する。
    /// </summary>
    /// <param name="category">オプションのカテゴリ名（省略時は "Dynamic"）</param>
    public DynamicOptionContainer(string category = "Dynamic")
    {
        _category = category;
    }

    /// <summary>動的に定義されたオプション一覧（OptionsEngine からの読み取り専用アクセス用）</summary>
    internal IReadOnlyList<OptionDescriptor> Options => _options;

    /// <summary>動的に定義されたアクション一覧（OptionsEngine からの読み取り専用アクセス用）</summary>
    internal IReadOnlyList<ActionDescriptor> Actions => _actions;

    /// <summary>
    /// bool 型オプションを追加する。
    /// UI ではチェックボックスとして表示される。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート</param>
    /// <param name="setter">値を設定するデリゲート</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddBool(string name, Func<bool> getter, Action<bool> setter, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            // "Dynamic.{カテゴリ}.{名前}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.Boolean,
            ValueType = typeof(bool),
            // object? 型との境界でラップして型を合わせる
            Getter = () => getter(),
            // null の場合は false にフォールバックしてキャストする
            Setter = v => setter((bool)(v ?? false)),
        });
        return this;
    }

    /// <summary>
    /// int 型オプションを追加する。
    /// min/max を指定するとスライダーとして表示され、省略するとテキスト入力になる。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート</param>
    /// <param name="setter">値を設定するデリゲート</param>
    /// <param name="min">スライダーの最小値（省略可）</param>
    /// <param name="max">スライダーの最大値（省略可）</param>
    /// <param name="step">スライダーのステップ値（デフォルト: 1）</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddInt(string name, Func<int> getter, Action<int> setter,
        double? min = null, double? max = null, double step = 1, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            // "Dynamic.{カテゴリ}.{名前}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.Integer,
            ValueType = typeof(int),
            // object? 型との境界でラップして型を合わせる
            Getter = () => getter(),
            // object? から int へ変換する（Convert.ToInt32 でボックス化された値も安全に変換）
            Setter = v => setter(Convert.ToInt32(v)),
            // min と max の両方が指定された場合のみ範囲制約を設定する
            Range = min.HasValue && max.HasValue ? new CRRangeAttribute(min.Value, max.Value) { Step = step } : null,
        });
        return this;
    }

    /// <summary>
    /// float 型オプションを追加する。
    /// min/max を指定するとスライダーとして表示され、省略するとテキスト入力になる。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート</param>
    /// <param name="setter">値を設定するデリゲート</param>
    /// <param name="min">スライダーの最小値（省略可）</param>
    /// <param name="max">スライダーの最大値（省略可）</param>
    /// <param name="step">スライダーのステップ値（デフォルト: 0.1）</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddFloat(string name, Func<float> getter, Action<float> setter,
        double? min = null, double? max = null, double step = 0.1, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            // "Dynamic.{カテゴリ}.{名前}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.Float,
            ValueType = typeof(float),
            // object? 型との境界でラップして型を合わせる
            Getter = () => getter(),
            // object? から float へ変換する（Convert.ToSingle でボックス化された値も安全に変換）
            Setter = v => setter(Convert.ToSingle(v)),
            // min と max の両方が指定された場合のみ範囲制約を設定する
            Range = min.HasValue && max.HasValue ? new CRRangeAttribute(min.Value, max.Value) { Step = step } : null,
        });
        return this;
    }

    /// <summary>
    /// string 型オプションを追加する。
    /// UI ではテキスト入力ボックスとして表示される。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート</param>
    /// <param name="setter">値を設定するデリゲート</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddString(string name, Func<string?> getter, Action<string?> setter, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            // "Dynamic.{カテゴリ}.{名前}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.String,
            ValueType = typeof(string),
            // object? 型との境界でラップして型を合わせる
            Getter = () => getter(),
            // object? を ToString() で文字列に変換する（null はそのまま渡す）
            Setter = v => setter(v?.ToString()),
        });
        return this;
    }

    /// <summary>
    /// ボタンアクションを追加する。
    /// UI ではボタンとして表示され、押下時に <paramref name="execute"/> が実行される。
    /// </summary>
    /// <param name="label">ボタンに表示するラベル</param>
    /// <param name="execute">ボタン押下時に実行するアクション</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddAction(string label, Action execute, int sortOrder = 0)
    {
        _actions.Add(new ActionDescriptor
        {
            // "Dynamic.{カテゴリ}.{ラベル}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{label}",
            Label = label,
            Category = _category,
            SortOrder = sortOrder,
            Execute = execute,
        });
        return this;
    }
}
