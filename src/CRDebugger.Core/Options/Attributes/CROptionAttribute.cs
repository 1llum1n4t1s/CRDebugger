namespace CRDebugger.Core.Options.Attributes;

/// <summary>
/// プロパティを CRDebugger の Options タブに表示するマーカーアトリビュート。
/// このアトリビュートがなくても全 public プロパティはスキャン対象になるが、
/// 将来的なフィルタリング用途として明示的な付与が推奨される。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CROptionAttribute : Attribute { }

/// <summary>
/// 数値プロパティのスライダー範囲制約を定義するアトリビュート。
/// <see cref="Min"/> / <see cref="Max"/> を指定すると UI がスライダーを表示し、
/// 省略するとテキスト入力ボックスが表示される。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CRRangeAttribute : Attribute
{
    /// <summary>スライダーの最小値</summary>
    public double Min { get; }

    /// <summary>スライダーの最大値</summary>
    public double Max { get; }

    /// <summary>スライダーのステップ値（1 目盛りの増減量。デフォルト: 1.0）</summary>
    public double Step { get; set; } = 1.0;

    /// <summary>
    /// 範囲制約を定義する。
    /// </summary>
    /// <param name="min">スライダーの最小値</param>
    /// <param name="max">スライダーの最大値</param>
    public CRRangeAttribute(double min, double max)
    {
        Min = min;
        Max = max;
    }
}

/// <summary>
/// プロパティまたはメソッドのカテゴリグループを指定するアトリビュート。
/// 同じカテゴリ名を持つ項目は Options タブで 1 つのセクションにまとめて表示される。
/// 指定しない場合は "General" カテゴリに分類される。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CRCategoryAttribute : Attribute
{
    /// <summary>所属するカテゴリの名前</summary>
    public string Name { get; }

    /// <summary>
    /// カテゴリグループを指定する。
    /// </summary>
    /// <param name="name">カテゴリ名</param>
    public CRCategoryAttribute(string name) => Name = name;
}

/// <summary>
/// プロパティまたはメソッドの UI 表示名をカスタマイズするアトリビュート。
/// 指定しない場合はプロパティ名をキャメルケース分割した文字列が使われる。
/// 例: "MaxRetryCount" → "Max Retry Count"
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CRDisplayNameAttribute : Attribute
{
    /// <summary>UI に表示するカスタム名</summary>
    public string Name { get; }

    /// <summary>
    /// 表示名を指定する。
    /// </summary>
    /// <param name="name">UI に表示するカスタム名</param>
    public CRDisplayNameAttribute(string name) => Name = name;
}

/// <summary>
/// プロパティまたはメソッドのカテゴリ内表示順を指定するアトリビュート。
/// 値が小さいほど先頭に表示される（昇順）。
/// 指定しない場合はソート順 0 として扱われる。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CRSortOrderAttribute : Attribute
{
    /// <summary>カテゴリ内のソート順（昇順。小さいほど先頭）</summary>
    public int Order { get; }

    /// <summary>
    /// 表示順序を指定する。
    /// </summary>
    /// <param name="order">カテゴリ内のソート順（昇順）</param>
    public CRSortOrderAttribute(int order) => Order = order;
}

/// <summary>
/// オプションコンテナクラスにメタデータを付与するアトリビュート。
/// <see cref="OptionsEngine"/> がコンテナクラスを識別する際に参照する。
/// グループ名・ソート順・表示有無を制御できる。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CRContainerAttribute : Attribute
{
    /// <summary>
    /// コンテナを分類するグループ名（省略可）。
    /// 複数のコンテナを論理的にまとめる際に使用する。
    /// </summary>
    public string? Group { get; set; }

    /// <summary>コンテナのソート順（昇順。小さいほど先頭に表示される）</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// このコンテナを UI に表示するかどうか（デフォルト: <c>true</c>）。
    /// <c>false</c> にするとスキャン対象から除外される。
    /// </summary>
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// 引数なし void メソッドをボタンとして Options タブに表示するアトリビュート。
/// ボタンを押すとそのメソッドが実行される。
/// 引数あり・void 以外のメソッドには効果がない。
/// Task 戻り値のメソッドにも対応し、非同期実行中はスピナーを表示する。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CRActionAttribute : Attribute
{
    /// <summary>
    /// ボタンに表示するラベル（省略時はメソッド名をキャメルケース分割した文字列を使用）。
    /// 例: "ResetDefaults" → "Reset Defaults"
    /// </summary>
    public string? Label { get; set; }
}

/// <summary>
/// プロパティまたはメソッドに説明テキストを付与するアトリビュート。
/// Options タブで表示名の下にサブテキストとして表示される。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CRDescriptionAttribute : Attribute
{
    /// <summary>説明テキスト</summary>
    public string Description { get; }

    /// <summary>
    /// 説明テキストを指定する。
    /// </summary>
    /// <param name="description">説明テキスト</param>
    public CRDescriptionAttribute(string description) => Description = description;
}

/// <summary>
/// string 型プロパティをカラーピッカーとして Options タブに表示するアトリビュート。
/// 値は "#RRGGBB" 形式の文字列として扱われる。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CRColorAttribute : Attribute { }
