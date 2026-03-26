using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CRDebugger.Core.Options;

namespace CRDebugger.Core.ViewModels;

public sealed class OptionsViewModel : ViewModelBase
{
    private readonly OptionsEngine _engine;

    public ObservableCollection<OptionCategoryViewModel> Categories { get; } = new();
    public ICommand RefreshCommand { get; }

    public OptionsViewModel(OptionsEngine engine)
    {
        _engine = engine;
        RefreshCommand = new RelayCommand(Refresh);
        _engine.ContainersChanged += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        Categories.Clear();
        var cats = _engine.ScanAll();
        foreach (var cat in cats)
        {
            Categories.Add(new OptionCategoryViewModel(cat));
        }
    }
}

public sealed class OptionCategoryViewModel : ViewModelBase
{
    public string Name { get; }
    public ObservableCollection<OptionItemViewModel> Items { get; } = new();

    public OptionCategoryViewModel(OptionCategory category)
    {
        Name = category.Name;
        foreach (var opt in category.Options)
            Items.Add(new OptionItemViewModel(opt));
        foreach (var act in category.Actions)
            Items.Add(new ActionItemViewModel(act));
    }
}

public class OptionItemViewModel : ViewModelBase
{
    private readonly OptionDescriptor _descriptor;

    public string DisplayName => _descriptor.DisplayName;
    public OptionKind Kind => _descriptor.Kind;
    public bool IsReadOnly => _descriptor.IsReadOnly;
    public double? Min => _descriptor.Range?.Min;
    public double? Max => _descriptor.Range?.Max;
    public double? Step => _descriptor.Range?.Step;
    public string[]? EnumNames => _descriptor.EnumNames;

    public object? Value
    {
        get => _descriptor.Getter();
        set
        {
            if (_descriptor.Setter != null)
            {
                _descriptor.Setter(ConvertValue(value));
                OnPropertyChanged();
            }
        }
    }

    public OptionItemViewModel(OptionDescriptor descriptor)
    {
        _descriptor = descriptor;

        // INotifyPropertyChangedを実装したコンテナの変更を監視
        // (OptionDescriptorのGetterが参照するインスタンスから)
    }

    private object? ConvertValue(object? value)
    {
        if (value == null) return null;
        var targetType = _descriptor.ValueType;

        if (targetType.IsEnum && value is string s)
            return Enum.Parse(targetType, s);

        return Convert.ChangeType(value, targetType);
    }
}

public sealed class ActionItemViewModel : OptionItemViewModel
{
    private readonly ActionDescriptor _action;

    public string Label => _action.Label;
    public ICommand ExecuteCommand { get; }

    public ActionItemViewModel(ActionDescriptor action)
        : base(new OptionDescriptor
        {
            Id = action.Id,
            DisplayName = action.Label,
            Category = action.Category,
            Kind = OptionKind.ReadOnly,
            ValueType = typeof(void)
        })
    {
        _action = action;
        ExecuteCommand = new RelayCommand(action.Execute);
    }
}
