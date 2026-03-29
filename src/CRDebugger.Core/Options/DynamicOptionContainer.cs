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
    /// UI ではトグルスイッチ（チェックボックス）として表示される。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート</param>
    /// <param name="setter">値を設定するデリゲート</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順。小さいほど先頭に表示される）</param>
    /// <param name="description">UI に表示する説明テキスト（省略可。null の場合は非表示）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddBool(string name, Func<bool> getter, Action<bool> setter,
        int sortOrder = 0, string? description = null)
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
            // 説明テキスト（null なら UI に表示しない）
            Description = description,
        });
        return this;
    }

    /// <summary>
    /// int 型オプションを追加する。
    /// min/max を指定するとスライダー付き数値入力として表示され、省略するとテキスト入力になる。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート</param>
    /// <param name="setter">値を設定するデリゲート</param>
    /// <param name="min">数値入力の最小値（省略可。max と両方指定で範囲制約が有効になる）</param>
    /// <param name="max">数値入力の最大値（省略可。min と両方指定で範囲制約が有効になる）</param>
    /// <param name="step">数値入力のステップ値（1 目盛りの増減量。デフォルト: 1）</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順。小さいほど先頭に表示される）</param>
    /// <param name="description">UI に表示する説明テキスト（省略可。null の場合は非表示）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddInt(string name, Func<int> getter, Action<int> setter,
        double? min = null, double? max = null, double step = 1, int sortOrder = 0,
        string? description = null)
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
            // 説明テキスト（null なら UI に表示しない）
            Description = description,
        });
        return this;
    }

    /// <summary>
    /// float 型オプションを追加する。
    /// min/max を指定するとスライダー付き数値入力として表示され、省略するとテキスト入力になる。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート</param>
    /// <param name="setter">値を設定するデリゲート</param>
    /// <param name="min">数値入力の最小値（省略可。max と両方指定で範囲制約が有効になる）</param>
    /// <param name="max">数値入力の最大値（省略可。min と両方指定で範囲制約が有効になる）</param>
    /// <param name="step">数値入力のステップ値（1 目盛りの増減量。デフォルト: 0.1）</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順。小さいほど先頭に表示される）</param>
    /// <param name="description">UI に表示する説明テキスト（省略可。null の場合は非表示）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddFloat(string name, Func<float> getter, Action<float> setter,
        double? min = null, double? max = null, double step = 0.1, int sortOrder = 0,
        string? description = null)
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
            // 説明テキスト（null なら UI に表示しない）
            Description = description,
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
    /// <param name="sortOrder">カテゴリ内の表示順（昇順。小さいほど先頭に表示される）</param>
    /// <param name="description">UI に表示する説明テキスト（省略可。null の場合は非表示）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddString(string name, Func<string?> getter, Action<string?> setter,
        int sortOrder = 0, string? description = null)
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
            // 説明テキスト（null なら UI に表示しない）
            Description = description,
        });
        return this;
    }

    /// <summary>
    /// カラーピッカーオプションを追加する。
    /// 値は "#RRGGBB" 形式の文字列として扱われる。
    /// UI ではカラースウォッチ＋HEXテキスト入力として表示される。
    /// </summary>
    /// <param name="name">オプション名（UI に表示される名前）</param>
    /// <param name="getter">現在値を取得するデリゲート（"#RRGGBB" 形式の文字列）</param>
    /// <param name="setter">値を設定するデリゲート（"#RRGGBB" 形式の文字列）</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順。小さいほど先頭に表示される）</param>
    /// <param name="description">UI に表示する説明テキスト（省略可。null の場合は非表示）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddColor(string name, Func<string?> getter, Action<string?> setter,
        int sortOrder = 0, string? description = null)
    {
        _options.Add(new OptionDescriptor
        {
            // "Dynamic.{カテゴリ}.{名前}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            // Color Kind はカラースウォッチ＋HEX入力の専用 UI を表示する
            Kind = OptionKind.Color,
            ValueType = typeof(string),
            // object? 型との境界でラップして型を合わせる
            Getter = () => getter(),
            // object? を ToString() で文字列に変換する（null はそのまま渡す）
            Setter = v => setter(v?.ToString()),
            // 説明テキスト（null なら UI に表示しない）
            Description = description,
        });
        return this;
    }

    /// <summary>
    /// 同期ボタンアクションを追加する。
    /// UI ではボタンとして表示され、押下時に <paramref name="execute"/> が実行される。
    /// 実行中はスピナーが表示され、完了後に成功/失敗のフィードバックアイコンが表示される。
    /// </summary>
    /// <param name="label">ボタンに表示するラベル</param>
    /// <param name="execute">ボタン押下時に実行する同期アクション</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順。小さいほど先頭に表示される）</param>
    /// <param name="description">UI に表示する説明テキスト（省略可。null の場合は非表示）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddAction(string label, Action execute, int sortOrder = 0,
        string? description = null)
    {
        _actions.Add(new ActionDescriptor
        {
            // "Dynamic.{カテゴリ}.{ラベル}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{label}",
            Label = label,
            Category = _category,
            SortOrder = sortOrder,
            // 同期アクションのデリゲート
            Execute = execute,
            // 同期メソッドを Task.CompletedTask 返却のラッパーで包んで非同期インターフェースに統一する
            ExecuteAsync = () => { execute(); return Task.CompletedTask; },
            // 説明テキスト（null なら UI に表示しない）
            Description = description,
        });
        return this;
    }

    /// <summary>
    /// 非同期ボタンアクションを追加する。
    /// UI ではボタンとして表示され、実行中はスピナーが表示される。
    /// Task が完了すると成功アイコン、例外が発生すると失敗アイコンが 2 秒間表示される。
    /// </summary>
    /// <param name="label">ボタンに表示するラベル</param>
    /// <param name="executeAsync">ボタン押下時に実行する非同期アクション</param>
    /// <param name="sortOrder">カテゴリ内の表示順（昇順。小さいほど先頭に表示される）</param>
    /// <param name="description">UI に表示する説明テキスト（省略可。null の場合は非表示）</param>
    /// <returns>メソッドチェーン用に自身を返す</returns>
    public DynamicOptionContainer AddAsyncAction(string label, Func<Task> executeAsync,
        int sortOrder = 0, string? description = null)
    {
        _actions.Add(new ActionDescriptor
        {
            // "Dynamic.{カテゴリ}.{ラベル}" 形式で一意の ID を生成する
            Id = $"Dynamic.{_category}.{label}",
            Label = label,
            Category = _category,
            SortOrder = sortOrder,
            // 非同期アクションを同期インターフェースにも提供する（fire-and-forget で呼べるように）
            Execute = () => executeAsync(),
            // 非同期アクションのデリゲート（ActionItemViewModel が await する）
            ExecuteAsync = executeAsync,
            // 説明テキスト（null なら UI に表示しない）
            Description = description,
        });
        return this;
    }
}
