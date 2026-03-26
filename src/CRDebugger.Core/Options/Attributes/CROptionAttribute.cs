namespace CRDebugger.Core.Options.Attributes;

/// <summary>
/// プロパティをCRDebugger Optionsタブに表示するマーカー
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CROptionAttribute : Attribute { }

/// <summary>
/// 数値プロパティの範囲制約
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CRRangeAttribute : Attribute
{
    public double Min { get; }
    public double Max { get; }
    public double Step { get; set; } = 1.0;

    public CRRangeAttribute(double min, double max)
    {
        Min = min;
        Max = max;
    }
}

/// <summary>
/// カテゴリグループ指定
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CRCategoryAttribute : Attribute
{
    public string Name { get; }
    public CRCategoryAttribute(string name) => Name = name;
}

/// <summary>
/// 表示名カスタマイズ
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CRDisplayNameAttribute : Attribute
{
    public string Name { get; }
    public CRDisplayNameAttribute(string name) => Name = name;
}

/// <summary>
/// 表示順序
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CRSortOrderAttribute : Attribute
{
    public int Order { get; }
    public CRSortOrderAttribute(int order) => Order = order;
}

/// <summary>
/// オプションコンテナクラスにメタデータを付与
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CRContainerAttribute : Attribute
{
    public string? Group { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// メソッドをボタンとして表示
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CRActionAttribute : Attribute
{
    public string? Label { get; set; }
}
