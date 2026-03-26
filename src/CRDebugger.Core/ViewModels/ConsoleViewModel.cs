using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Logging;

namespace CRDebugger.Core.ViewModels;

public sealed class ConsoleViewModel : ViewModelBase
{
    private readonly LogStore _logStore;
    private readonly IUiThread _uiThread;
    private LogEntry? _selectedEntry;
    private bool _showDebug = true;
    private bool _showInfo = true;
    private bool _showWarning = true;
    private bool _showError = true;
    private string _searchText = string.Empty;
    private int _debugCount;
    private int _infoCount;
    private int _warningCount;
    private int _errorCount;

    public ObservableCollection<LogEntry> DisplayEntries { get; } = new();

    public LogEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public bool ShowDebug
    {
        get => _showDebug;
        set { if (SetProperty(ref _showDebug, value)) RefreshFilter(); }
    }

    public bool ShowInfo
    {
        get => _showInfo;
        set { if (SetProperty(ref _showInfo, value)) RefreshFilter(); }
    }

    public bool ShowWarning
    {
        get => _showWarning;
        set { if (SetProperty(ref _showWarning, value)) RefreshFilter(); }
    }

    public bool ShowError
    {
        get => _showError;
        set { if (SetProperty(ref _showError, value)) RefreshFilter(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshFilter(); }
    }

    public int DebugCount { get => _debugCount; private set => SetProperty(ref _debugCount, value); }
    public int InfoCount { get => _infoCount; private set => SetProperty(ref _infoCount, value); }
    public int WarningCount { get => _warningCount; private set => SetProperty(ref _warningCount, value); }
    public int ErrorCount { get => _errorCount; private set => SetProperty(ref _errorCount, value); }

    public ICommand ClearCommand { get; }

    public ConsoleViewModel(LogStore logStore, IUiThread uiThread)
    {
        _logStore = logStore;
        _uiThread = uiThread;
        ClearCommand = new RelayCommand(Clear);
        _logStore.EntryAdded += OnEntryAdded;

        // 既存ログをロード
        RefreshFilter();
    }

    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        _uiThread.Invoke(() =>
        {
            UpdateCount(entry.Level, 1);
            var filter = CreateFilter();
            if (filter.Matches(entry))
            {
                DisplayEntries.Add(entry);
            }
        });
    }

    private void RefreshFilter()
    {
        DisplayEntries.Clear();
        var filter = CreateFilter();
        var entries = _logStore.GetFiltered(filter);
        foreach (var entry in entries)
            DisplayEntries.Add(entry);

        var counts = _logStore.GetCounts();
        DebugCount = counts.Debug;
        InfoCount = counts.Info;
        WarningCount = counts.Warning;
        ErrorCount = counts.Error;
    }

    private void Clear()
    {
        _logStore.Clear();
        DisplayEntries.Clear();
        DebugCount = 0;
        InfoCount = 0;
        WarningCount = 0;
        ErrorCount = 0;
        SelectedEntry = null;
    }

    private void UpdateCount(CRLogLevel level, int delta)
    {
        switch (level)
        {
            case CRLogLevel.Debug: DebugCount += delta; break;
            case CRLogLevel.Info: InfoCount += delta; break;
            case CRLogLevel.Warning: WarningCount += delta; break;
            case CRLogLevel.Error: ErrorCount += delta; break;
        }
    }

    private LogFilter CreateFilter() => new(ShowDebug, ShowInfo, ShowWarning, ShowError,
        string.IsNullOrEmpty(SearchText) ? null : SearchText);
}
