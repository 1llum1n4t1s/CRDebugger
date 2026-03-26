using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Profiler;

namespace CRDebugger.Core.ViewModels;

public sealed class ProfilerViewModel : ViewModelBase
{
    private readonly ProfilerEngine _engine;
    private readonly IUiThread _uiThread;
    private double _fps;
    private string _workingSet = "0 MB";
    private string _privateMemory = "0 MB";
    private string _gcMemory = "0 MB";
    private string _gen0;
    private string _gen1;
    private string _gen2;

    public double Fps { get => _fps; private set => SetProperty(ref _fps, value); }
    public string WorkingSet { get => _workingSet; private set => SetProperty(ref _workingSet, value); }
    public string PrivateMemory { get => _privateMemory; private set => SetProperty(ref _privateMemory, value); }
    public string GcMemory { get => _gcMemory; private set => SetProperty(ref _gcMemory, value); }
    public string Gen0 { get => _gen0; private set => SetProperty(ref _gen0, value); }
    public string Gen1 { get => _gen1; private set => SetProperty(ref _gen1, value); }
    public string Gen2 { get => _gen2; private set => SetProperty(ref _gen2, value); }

    /// <summary>メモリ使用量の履歴（グラフ描画用）</summary>
    public ObservableCollection<double> MemoryHistory { get; } = new();
    /// <summary>FPS履歴（グラフ描画用）</summary>
    public ObservableCollection<double> FpsHistory { get; } = new();

    public ICommand GcCollectCommand { get; }

    public ProfilerViewModel(ProfilerEngine engine, IUiThread uiThread)
    {
        _engine = engine;
        _uiThread = uiThread;
        _gen0 = "0"; _gen1 = "0"; _gen2 = "0";
        GcCollectCommand = new RelayCommand(() =>
        {
            _engine.ForceGarbageCollection();
        });
        _engine.SnapshotTaken += OnSnapshot;
    }

    private void OnSnapshot(object? sender, ProfilerSnapshot snap)
    {
        _uiThread.Invoke(() =>
        {
            Fps = snap.FpsEstimate;
            WorkingSet = FormatBytes(snap.WorkingSetBytes);
            PrivateMemory = FormatBytes(snap.PrivateMemoryBytes);
            GcMemory = FormatBytes(snap.GcTotalMemoryBytes);
            Gen0 = snap.Gen0Collections.ToString();
            Gen1 = snap.Gen1Collections.ToString();
            Gen2 = snap.Gen2Collections.ToString();

            FpsHistory.Add(snap.FpsEstimate);
            if (FpsHistory.Count > ProfilerEngine.MaxHistorySize)
                FpsHistory.RemoveAt(0);

            var memMb = snap.WorkingSetBytes / (1024.0 * 1024.0);
            MemoryHistory.Add(memMb);
            if (MemoryHistory.Count > ProfilerEngine.MaxHistorySize)
                MemoryHistory.RemoveAt(0);
        });
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
            >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
