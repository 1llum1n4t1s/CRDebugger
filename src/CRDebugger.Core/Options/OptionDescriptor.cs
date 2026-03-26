using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Options;

/// <summary>
/// プロパティの型（UIコントロール決定用）
/// </summary>
public enum OptionKind
{
    Boolean,
    Integer,
    Float,
    String,
    Enum,
    ReadOnly
}

/// <summary>
/// リフレクションで解析されたオプションプロパティの記述子
/// </summary>
public sealed class OptionDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public int SortOrder { get; init; }
    public OptionKind Kind { get; init; }
    public Type ValueType { get; init; } = typeof(object);
    public Func<object?> Getter { get; init; } = () => null;
    public Action<object?>? Setter { get; init; }
    public bool IsReadOnly => Setter == null;
    public CRRangeAttribute? Range { get; init; }
    public string[]? EnumNames { get; init; }
}

/// <summary>
/// メソッド→ボタンの記述子
/// </summary>
public sealed class ActionDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public int SortOrder { get; init; }
    public Action Execute { get; init; } = () => { };
}

/// <summary>
/// カテゴリでグループ化されたオプション群
/// </summary>
public sealed class OptionCategory
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<OptionDescriptor> Options { get; init; } = Array.Empty<OptionDescriptor>();
    public IReadOnlyList<ActionDescriptor> Actions { get; init; } = Array.Empty<ActionDescriptor>();
}
