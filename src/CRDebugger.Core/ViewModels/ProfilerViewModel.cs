using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Profiler;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// プロファイラータブのViewModel。
/// メモリ使用量・FPS・GCコレクション数等のパフォーマンスメトリクスをリアルタイムで表示する。
/// <see cref="ProfilerEngine"/> のスナップショットイベントを購読し、UIスレッド上でデータを更新する。
/// </summary>
public sealed class ProfilerViewModel : ViewModelBase
{
    /// <summary>パフォーマンス計測・スナップショット配信を担うプロファイラーエンジン</summary>
    private readonly ProfilerEngine _engine;

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
        _engine = engine;
        _uiThread = uiThread;
        // GCコレクション回数の初期値を "0" に設定
        _gen0 = "0"; _gen1 = "0"; _gen2 = "0";
        // GC強制実行コマンドを定義
        GcCollectCommand = new RelayCommand(() =>
        {
            _engine.ForceGarbageCollection();
        });

        // 定期スナップショットの受信ハンドラを登録
        _engine.SnapshotTaken += OnSnapshot;
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

            // FPS履歴を末尾に追加し、上限を超えた分を先頭から削除
            FpsHistory.Add(snap.FpsEstimate);
            while (FpsHistory.Count > ProfilerEngine.MaxHistorySize)
                FpsHistory.RemoveAt(0);

            // メモリ使用量をMB単位に変換して履歴に追加し、上限超過分を削除
            var memMb = snap.WorkingSetBytes / (1024.0 * 1024.0);
            MemoryHistory.Add(memMb);
            while (MemoryHistory.Count > ProfilerEngine.MaxHistorySize)
                MemoryHistory.RemoveAt(0);
        });
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
}
