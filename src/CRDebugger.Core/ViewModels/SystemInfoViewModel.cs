using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.SystemInfo;

namespace CRDebugger.Core.ViewModels;

public sealed class SystemInfoViewModel : ViewModelBase
{
    private readonly SystemInfoCollector _collector;

    public ObservableCollection<SystemInfoGroup> Groups { get; } = new();
    public ICommand RefreshCommand { get; }

    public SystemInfoViewModel(SystemInfoCollector collector)
    {
        _collector = collector;
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public void Refresh()
    {
        Groups.Clear();
        var entries = _collector.CollectAll();
        var groups = entries.GroupBy(e => e.Category);
        foreach (var group in groups)
        {
            Groups.Add(new SystemInfoGroup(
                group.Key,
                group.Select(e => new SystemInfoItem(e.Key, e.Value)).ToList()
            ));
        }
    }
}

public sealed record SystemInfoGroup(string Category, IReadOnlyList<SystemInfoItem> Items);
public sealed record SystemInfoItem(string Key, string Value);
