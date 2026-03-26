using System.Linq.Expressions;
using System.Reflection;
using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Options;

/// <summary>
/// リフレクションでオブジェクトからオプションを自動検出するエンジン
/// </summary>
public sealed class OptionsEngine
{
    private readonly List<object> _containers = new();
    private readonly object _lock = new();

    public event EventHandler? ContainersChanged;

    public void AddContainer(object container)
    {
        lock (_lock)
        {
            _containers.Add(container);
        }
        ContainersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveContainer(object container)
    {
        lock (_lock)
        {
            _containers.Remove(container);
        }
        ContainersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 全コンテナをスキャンしてカテゴリ別にグループ化
    /// </summary>
    public IReadOnlyList<OptionCategory> ScanAll()
    {
        var options = new List<OptionDescriptor>();
        var actions = new List<ActionDescriptor>();

        List<object> snapshot;
        lock (_lock) { snapshot = _containers.ToList(); }

        foreach (var container in snapshot)
        {
            // DynamicOptionContainerは専用のスキャンロジック
            if (container is DynamicOptionContainer dynamic)
            {
                options.AddRange(dynamic.Options);
                actions.AddRange(dynamic.Actions);
                continue;
            }

            ScanProperties(container, options);
            ScanMethods(container, actions);
        }

        // カテゴリ別にグループ化（O(n)のGroupByで最適化）
        var optionsByCategory = options.GroupBy(o => o.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OptionDescriptor>)g.OrderBy(o => o.SortOrder).ToList());
        var actionsByCategory = actions.GroupBy(a => a.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ActionDescriptor>)g.OrderBy(a => a.SortOrder).ToList());

        var categoryNames = optionsByCategory.Keys
            .Concat(actionsByCategory.Keys)
            .Distinct()
            .OrderBy(c => c);

        return categoryNames.Select(name => new OptionCategory
        {
            Name = name,
            Options = optionsByCategory.GetValueOrDefault(name, Array.Empty<OptionDescriptor>()),
            Actions = actionsByCategory.GetValueOrDefault(name, Array.Empty<ActionDescriptor>())
        }).ToList();
    }

    private static void ScanProperties(object container, List<OptionDescriptor> results)
    {
        var type = container.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            // CROptionAttributeがなくても全publicプロパティを対象にする
            // (SRDebuggerと同じ挙動)
            if (!IsSupportedType(prop.PropertyType)) continue;

            var displayName = prop.GetCustomAttribute<CRDisplayNameAttribute>()?.Name
                ?? SplitCamelCase(prop.Name);
            var category = prop.GetCustomAttribute<CRCategoryAttribute>()?.Name ?? "General";
            var sortOrder = prop.GetCustomAttribute<CRSortOrderAttribute>()?.Order ?? 0;
            var range = prop.GetCustomAttribute<CRRangeAttribute>();

            var getter = CreateGetter(container, prop);
            var setter = prop.CanWrite ? CreateSetter(container, prop) : null;

            results.Add(new OptionDescriptor
            {
                Id = $"{type.FullName}.{prop.Name}",
                DisplayName = displayName,
                Category = category,
                SortOrder = sortOrder,
                Kind = ResolveKind(prop.PropertyType, setter == null),
                ValueType = prop.PropertyType,
                Getter = getter,
                Setter = setter,
                Range = range,
                EnumNames = prop.PropertyType.IsEnum ? Enum.GetNames(prop.PropertyType) : null
            });
        }
    }

    private static void ScanMethods(object container, List<ActionDescriptor> results)
    {
        var type = container.GetType();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var actionAttr = method.GetCustomAttribute<CRActionAttribute>();
            if (actionAttr == null) continue;
            if (method.GetParameters().Length > 0) continue;
            if (method.ReturnType != typeof(void)) continue;

            var label = actionAttr.Label
                ?? method.GetCustomAttribute<CRDisplayNameAttribute>()?.Name
                ?? SplitCamelCase(method.Name);
            var category = method.GetCustomAttribute<CRCategoryAttribute>()?.Name ?? "General";
            var sortOrder = method.GetCustomAttribute<CRSortOrderAttribute>()?.Order ?? 0;

            var target = container;
            var m = method;
            results.Add(new ActionDescriptor
            {
                Id = $"{type.FullName}.{method.Name}",
                Label = label,
                Category = category,
                SortOrder = sortOrder,
                Execute = () => m.Invoke(target, null)
            });
        }
    }

    private static Func<object?> CreateGetter(object target, PropertyInfo prop)
    {
        // Expression.Compile でパフォーマンス最適化
        var instance = Expression.Constant(target);
        var access = Expression.Property(instance, prop);
        var convert = Expression.Convert(access, typeof(object));
        var lambda = Expression.Lambda<Func<object?>>(convert);
        return lambda.Compile();
    }

    private static Action<object?> CreateSetter(object target, PropertyInfo prop)
    {
        var instance = Expression.Constant(target);
        var param = Expression.Parameter(typeof(object), "value");
        var convert = Expression.Convert(param, prop.PropertyType);
        var assign = Expression.Assign(Expression.Property(instance, prop), convert);
        var lambda = Expression.Lambda<Action<object?>>(assign, param);
        return lambda.Compile();
    }

    private static OptionKind ResolveKind(Type type, bool isReadOnly)
    {
        if (isReadOnly) return OptionKind.ReadOnly;
        if (type == typeof(bool)) return OptionKind.Boolean;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
            type == typeof(byte) || type == typeof(uint) || type == typeof(ushort) ||
            type == typeof(sbyte)) return OptionKind.Integer;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return OptionKind.Float;
        if (type == typeof(string)) return OptionKind.String;
        if (type.IsEnum) return OptionKind.Enum;
        return OptionKind.ReadOnly;
    }

    private static bool IsSupportedType(Type type)
    {
        return type == typeof(bool) || type == typeof(int) || type == typeof(long) ||
               type == typeof(short) || type == typeof(byte) || type == typeof(uint) ||
               type == typeof(ushort) || type == typeof(sbyte) || type == typeof(float) ||
               type == typeof(double) || type == typeof(decimal) || type == typeof(string) ||
               type.IsEnum;
    }

    private static readonly System.Text.RegularExpressions.Regex CamelCaseRegex =
        new("([a-z])([A-Z])", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string SplitCamelCase(string input) =>
        CamelCaseRegex.Replace(input, "$1 $2");
}
