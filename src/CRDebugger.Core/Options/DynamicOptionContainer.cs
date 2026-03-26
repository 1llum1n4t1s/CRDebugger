using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Options;

/// <summary>
/// コードからオプションを動的に定義するコンテナ
/// </summary>
public sealed class DynamicOptionContainer
{
    private readonly List<OptionDescriptor> _options = new();
    private readonly List<ActionDescriptor> _actions = new();
    private readonly string _category;

    public DynamicOptionContainer(string category = "Dynamic")
    {
        _category = category;
    }

    /// <summary>動的に定義されたオプション一覧</summary>
    internal IReadOnlyList<OptionDescriptor> Options => _options;
    /// <summary>動的に定義されたアクション一覧</summary>
    internal IReadOnlyList<ActionDescriptor> Actions => _actions;

    /// <summary>bool型オプションを追加</summary>
    public DynamicOptionContainer AddBool(string name, Func<bool> getter, Action<bool> setter, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.Boolean,
            ValueType = typeof(bool),
            Getter = () => getter(),
            Setter = v => setter((bool)(v ?? false)),
        });
        return this;
    }

    /// <summary>int型オプションを追加</summary>
    public DynamicOptionContainer AddInt(string name, Func<int> getter, Action<int> setter,
        double? min = null, double? max = null, double step = 1, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.Integer,
            ValueType = typeof(int),
            Getter = () => getter(),
            Setter = v => setter(Convert.ToInt32(v)),
            Range = min.HasValue && max.HasValue ? new CRRangeAttribute(min.Value, max.Value) { Step = step } : null,
        });
        return this;
    }

    /// <summary>float型オプションを追加</summary>
    public DynamicOptionContainer AddFloat(string name, Func<float> getter, Action<float> setter,
        double? min = null, double? max = null, double step = 0.1, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.Float,
            ValueType = typeof(float),
            Getter = () => getter(),
            Setter = v => setter(Convert.ToSingle(v)),
            Range = min.HasValue && max.HasValue ? new CRRangeAttribute(min.Value, max.Value) { Step = step } : null,
        });
        return this;
    }

    /// <summary>string型オプションを追加</summary>
    public DynamicOptionContainer AddString(string name, Func<string?> getter, Action<string?> setter, int sortOrder = 0)
    {
        _options.Add(new OptionDescriptor
        {
            Id = $"Dynamic.{_category}.{name}",
            DisplayName = name,
            Category = _category,
            SortOrder = sortOrder,
            Kind = OptionKind.String,
            ValueType = typeof(string),
            Getter = () => getter(),
            Setter = v => setter(v?.ToString()),
        });
        return this;
    }

    /// <summary>ボタンアクションを追加</summary>
    public DynamicOptionContainer AddAction(string label, Action execute, int sortOrder = 0)
    {
        _actions.Add(new ActionDescriptor
        {
            Id = $"Dynamic.{_category}.{label}",
            Label = label,
            Category = _category,
            SortOrder = sortOrder,
            Execute = execute,
        });
        return this;
    }
}
