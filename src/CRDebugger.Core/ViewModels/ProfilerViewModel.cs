using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Profiler;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// プロファイラータブのViewModel。
/// メモリ使用量・FPS・GCコレクション数・CPU使用率・CPUホットスポット等の
/// パフォーマンスメトリクスをリアルタイムで表示する。
/// <see cref="ProfilerEngine"/> のスナップショットイベントを購読し、UIスレッド上でデータを更新する。
/// </summary>
public sealed class ProfilerViewModel : ViewModelBase
{
    /// <summary>パフォーマンス計測・スナップショット配信を担うプロファイラーエンジン</summary>
    private readonly ProfilerEngine _profiler;

    /// <summary>UIスレッドへの処理マーシャリングインターフェース</summary>
    private readonly IUiThread _uiThread;

    /// <summary>FPS推定値（バッキングフィールド）</summary>
    private double _fps;

    /// <summary>ワーキングセット表示文字列（バッキングフィールド）</summary>
    private string _workingSet = "0 MB";

    /// <summary>プライベートメモリ表示文字列（バッキングフィールド）</summary>
    private string _privateMemory = "0 MB";

    /// <summary>GC管理メモリ表示文字列（バッキングフィールド）</summary>
    private string _gcMemory = "0 MB";

    /// <summary>Gen0 GCコレクション回数文字列（バッキングフィールド）</summary>
    private string _gen0;

    /// <summary>Gen1 GCコレクション回数文字列（バッキングフィールド）</summary>
    private string _gen1;

    /// <summary>Gen2 GCコレクション回数文字列（バッキングフィールド）</summary>
    private string _gen2;

    /// <summary>CPU使用率（バッキングフィールド）</summary>
    private double _cpuUsagePercent;

    /// <summary>CPU使用率表示文字列（バッキングフィールド）</summary>
    private string _cpuUsageDisplay = "0.0%";

    /// <summary>CPUホットスポットが存在するかどうか（バッキングフィールド）</summary>
    private bool _hasHotspots;

    /// <summary>メモリホットスポットが存在するかどうか（バッキングフィールド）</summary>
    private bool _hasMemoryHotspots;

    /// <summary>スナップショット受信回数（ホットスポット更新間隔の制御用）</summary>
    private int _snapshotCount;

    /// <summary>
    /// 現在のFPS（フレーム/秒）推定値。
    /// グラフ描画用の <see cref="FpsHistory"/> にも同時に追記される。
    /// </summary>
    public double Fps { get => _fps; private set => SetProperty(ref _fps, value); }

    /// <summary>
    /// ワーキングセット（プロセスが使用している物理メモリ量）の人間可読な表示文字列。
    /// 例: "128.5 MB"
    /// </summary>
    public string WorkingSet { get => _workingSet; private set => SetProperty(ref _workingSet, value); }

    /// <summary>
    /// プロセスのプライベートメモリ使用量の人間可読な表示文字列。
    /// 例: "256.0 MB"
    /// </summary>
    public string PrivateMemory { get => _privateMemory; private set => SetProperty(ref _privateMemory, value); }

    /// <summary>
    /// GCが管理するマネージドメモリ総量の人間可読な表示文字列。
    /// 例: "64.3 MB"
    /// </summary>
    public string GcMemory { get => _gcMemory; private set => SetProperty(ref _gcMemory, value); }

    /// <summary>
    /// Gen0 GCコレクションの累計実行回数を文字列で表したもの。
    /// 頻度が高いほど短命なオブジェクトが多いことを示す。
    /// </summary>
    public string Gen0 { get => _gen0; private set => SetProperty(ref _gen0, value); }

    /// <summary>
    /// Gen1 GCコレクションの累計実行回数を文字列で表したもの。
    /// </summary>
    public string Gen1 { get => _gen1; private set => SetProperty(ref _gen1, value); }

    /// <summary>
    /// Gen2 GCコレクションの累計実行回数を文字列で表したもの。
    /// フルGCの頻度を示す重要な指標。
    /// </summary>
    public string Gen2 { get => _gen2; private set => SetProperty(ref _gen2, value); }

    /// <summary>
    /// プロセスCPU使用率（0〜100%）
    /// </summary>
    public double CpuUsagePercent { get => _cpuUsagePercent; private set => SetProperty(ref _cpuUsagePercent, value); }

    /// <summary>
    /// CPU使用率の表示文字列。例: "23.5%"
    /// </summary>
    public string CpuUsageDisplay { get => _cpuUsageDisplay; private set => SetProperty(ref _cpuUsageDisplay, value); }

    /// <summary>
    /// CPUホットスポットが存在する場合は true
    /// </summary>
    public bool HasHotspots { get => _hasHotspots; private set => SetProperty(ref _hasHotspots, value); }

    /// <summary>
    /// メモリホットスポットが存在する場合は true
    /// </summary>
    public bool HasMemoryHotspots { get => _hasMemoryHotspots; private set => SetProperty(ref _hasMemoryHotspots, value); }

    /// <summary>
    /// メモリ使用量の時系列履歴（MB単位）。
    /// グラフコントロールへのバインドに使用する。
    /// 上限は <see cref="ProfilerEngine.MaxHistorySize"/> で制御される。
    /// </summary>
    public ObservableCollection<double> MemoryHistory { get; } = new();

    /// <summary>
    /// FPS値の時系列履歴。
    /// グラフコントロールへのバインドに使用する。
    /// 上限は <see cref="ProfilerEngine.MaxHistorySize"/> で制御される。
    /// </summary>
    public ObservableCollection<double> FpsHistory { get; } = new();

    /// <summary>
    /// CPU使用率の時系列履歴（%単位）。
    /// 上限は <see cref="ProfilerEngine.MaxHistorySize"/> で制御される。
    /// </summary>
    public ObservableCollection<double> CpuHistory { get; } = new();

    /// <summary>
    /// CPU時間上位のホットスポット一覧。
    /// 5回のスナップショットごとに更新される。
    /// </summary>
    public ObservableCollection<HotspotItem> CpuHotspots { get; } = new();

    /// <summary>
    /// メモリ使用量上位のホットスポット一覧。
    /// 5回のスナップショットごとに更新される。
    /// </summary>
    public ObservableCollection<MemoryHotspotItem> MemoryHotspots { get; } = new();

    /// <summary>
    /// <c>GC.Collect()</c> を強制実行するコマンド。
    /// メモリ解放の動作確認やデバッグ用途に使用する。
    /// </summary>
    public ICommand GcCollectCommand { get; }

    /// <summary>
    /// <see cref="ProfilerViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="engine">パフォーマンス計測・スナップショット配信を担うプロファイラーエンジン</param>
    /// <param name="uiThread">UIスレッドへの処理マーシャリングインターフェース</param>
    public ProfilerViewModel(ProfilerEngine engine, IUiThread uiThread)
    {
        _profiler = engine;
        _uiThread = uiThread;
        // GCコレクション回数の初期値を "0" に設定
        _gen0 = "0"; _gen1 = "0"; _gen2 = "0";
        // GC強制実行コマンドを定義
        GcCollectCommand = new RelayCommand(() =>
        {
            _profiler.ForceGarbageCollection();
        });

        // 定期スナップショットの受信ハンドラを登録
        _profiler.SnapshotTaken += OnSnapshot;
    }

    /// <summary>
    /// プロファイラーエンジンからスナップショットを受信した際にUI表示を更新する。
    /// UIスレッドへのマーシャリングを行ったうえで各プロパティとグラフ履歴を更新する。
    /// </summary>
    /// <param name="sender">イベント発生元（<see cref="ProfilerEngine"/>）</param>
    /// <param name="snap">受信したパフォーマンススナップショット</param>
    private void OnSnapshot(object? sender, ProfilerSnapshot snap)
    {
        // バックグラウンドスレッドからの呼び出しをUIスレッドへマーシャリング
        _uiThread.Invoke(() =>
        {
            // 各メトリクスのプロパティを最新値で更新
            Fps = snap.FpsEstimate;
            WorkingSet = FormatBytes(snap.WorkingSetBytes);
            PrivateMemory = FormatBytes(snap.PrivateMemoryBytes);
            GcMemory = FormatBytes(snap.GcTotalMemoryBytes);
            Gen0 = snap.Gen0Collections.ToString();
            Gen1 = snap.Gen1Collections.ToString();
            Gen2 = snap.Gen2Collections.ToString();

            // CPU使用率を更新
            CpuUsagePercent = snap.CpuUsagePercent;
            CpuUsageDisplay = $"{snap.CpuUsagePercent:F1}%";

            // 各履歴コレクションに値を追加し、上限を超えた分を先頭から削除
            AppendWithLimit(FpsHistory, snap.FpsEstimate, ProfilerEngine.MaxHistorySize);
            var memMb = snap.WorkingSetBytes / (1024.0 * 1024.0);
            AppendWithLimit(MemoryHistory, memMb, ProfilerEngine.MaxHistorySize);
            AppendWithLimit(CpuHistory, snap.CpuUsagePercent, ProfilerEngine.MaxHistorySize);

            // 5回のスナップショットごとにホットスポットを更新（パフォーマンス対策）
            _snapshotCount++;
            if (_snapshotCount % 5 == 0)
            {
                UpdateHotspots();
            }
        });
    }

    /// <summary>
    /// CPUホットスポットとメモリホットスポット情報を更新する。
    /// OperationTracker から各指標の上位操作を取得し、表示用に変換する。
    /// </summary>
    private void UpdateHotspots()
    {
        // CPUホットスポットの更新
        var hotspots = _profiler.Operations.GetCpuHotspots(10);
        var totalCpu = hotspots.Sum(h => h.TotalCpuTime.TotalMilliseconds);

        CpuHotspots.Clear();
        foreach (var m in hotspots)
        {
            // 各操作のCPU時間を全体に対する割合で算出
            var pct = totalCpu > 0 ? (m.TotalCpuTime.TotalMilliseconds / totalCpu) * 100 : 0;
            CpuHotspots.Add(new HotspotItem(
                m.OperationName, m.Category, m.InvocationCount,
                FormatTime(m.TotalCpuTime), FormatTime(m.AverageCpuTime), pct));
        }
        HasHotspots = CpuHotspots.Count > 0;

        // メモリホットスポットの更新
        var memHotspots = _profiler.Operations.GetMemoryHotspots(10);
        var totalMem = memHotspots.Sum(h => Math.Abs(h.TotalMemoryDelta));

        MemoryHotspots.Clear();
        foreach (var m in memHotspots)
        {
            // 各操作のメモリ使用量を全体に対する割合で算出
            var pct = totalMem > 0 ? (Math.Abs(m.TotalMemoryDelta) / (double)totalMem) * 100 : 0;
            MemoryHotspots.Add(new MemoryHotspotItem(
                m.OperationName, m.Category, m.InvocationCount,
                FormatBytes(Math.Abs(m.TotalMemoryDelta)),
                FormatBytes(Math.Abs(m.MaxMemoryDelta)), pct));
        }
        HasMemoryHotspots = MemoryHotspots.Count > 0;
    }

    /// <summary>
    /// ObservableCollection に値を追加し、上限を超えた分を先頭から削除する。
    /// FpsHistory / MemoryHistory / CpuHistory の共通トリムロジック。
    /// </summary>
    /// <param name="collection">追加対象のコレクション</param>
    /// <param name="value">追加する値</param>
    /// <param name="maxSize">コレクションの最大保持件数</param>
    private static void AppendWithLimit(ObservableCollection<double> collection, double value, int maxSize)
    {
        collection.Add(value);
        while (collection.Count > maxSize)
            collection.RemoveAt(0);
    }

    /// <summary>
    /// バイト数を人間が読みやすい単位（B / KB / MB / GB）に変換して文字列で返す
    /// </summary>
    /// <param name="bytes">変換するバイト数（0以上）</param>
    /// <returns>単位付きの文字列（例: "1.5 GB", "128.5 MB", "64.0 KB", "512 B"）</returns>
    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            // 1GB以上はGB単位で小数点1桁表示
            >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
            // 1MB以上はMB単位で小数点1桁表示
            >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            // 1KB以上はKB単位で小数点1桁表示
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            // 1KB未満はバイト数をそのまま表示
            _ => $"{bytes} B"
        };
    }

    /// <summary>
    /// TimeSpan を人間が読みやすい時間表記に変換する
    /// </summary>
    /// <param name="ts">変換する時間</param>
    /// <returns>時間の文字列表記（例: "1.23s", "45.6ms", "0.12ms"）</returns>
    private static string FormatTime(TimeSpan ts)
    {
        return ts.TotalSeconds switch
        {
            >= 1 => $"{ts.TotalSeconds:F2}s",
            >= 0.001 => $"{ts.TotalMilliseconds:F1}ms",
            _ => $"{ts.TotalMilliseconds:F2}ms"
        };
    }
}

/// <summary>
/// CPUホットスポットの1行分を表すイミュータブルなレコード型。
/// ProfilerView のリスト表示用。
/// </summary>
/// <param name="OperationName">操作名</param>
/// <param name="Category">カテゴリ名</param>
/// <param name="InvocationCount">呼び出し回数</param>
/// <param name="TotalCpuTime">合計CPU時間（フォーマット済み）</param>
/// <param name="AverageCpuTime">平均CPU時間（フォーマット済み）</param>
/// <param name="CpuPercent">全体に対するCPU時間の割合（%）</param>
public sealed record HotspotItem(
    string OperationName, string Category, long InvocationCount,
    string TotalCpuTime, string AverageCpuTime, double CpuPercent);

/// <summary>
/// メモリホットスポットの1行分を表すイミュータブルなレコード型。
/// ProfilerView のリスト表示用。
/// </summary>
/// <param name="OperationName">操作名</param>
/// <param name="Category">カテゴリ名</param>
/// <param name="InvocationCount">呼び出し回数</param>
/// <param name="TotalMemoryDelta">合計メモリ増減量（フォーマット済み）</param>
/// <param name="MaxMemoryDelta">最大メモリ増減量（フォーマット済み）</param>
/// <param name="MemoryPercent">全体に対するメモリ使用量の割合（%）</param>
public sealed record MemoryHotspotItem(
    string OperationName, string Category, long InvocationCount,
    string TotalMemoryDelta, string MaxMemoryDelta, double MemoryPercent);
