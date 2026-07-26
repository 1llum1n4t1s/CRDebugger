using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Logging;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// コンソール（ログ）タブのViewModel。
/// ログエントリの表示・フィルタリング・検索機能を提供する。
/// ログストアの変更イベントを購読し、UIをリアルタイムで更新する。
/// 大量の連続ログを 16ms ごとにバッチして UI スレッドへフラッシュし、
/// 1 件ずつ Invoke する場合と比べてディスパッチコストを大幅削減する。
/// </summary>
public sealed class ConsoleViewModel : ViewModelBase
{
    /// <summary>UI フラッシュ間隔（約 60fps 相当）</summary>
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>ログストアへの参照（ログデータのソース）</summary>
    private readonly LogStore _logStore;

    /// <summary>UIスレッドマーシャリング用インターフェース</summary>
    private readonly IUiThread _uiThread;

    /// <summary>追加待ちエントリのキュー（バッチング用、ロックフリー）</summary>
    private readonly ConcurrentQueue<LogEntry> _pendingAdds = new();

    /// <summary>更新待ちエントリのキュー（バッチング用、ロックフリー）</summary>
    private readonly ConcurrentQueue<LogEntry> _pendingUpdates = new();

    /// <summary>定期フラッシュタイマー（System.Threading.Timer を完全修飾で使用してWinFormsのTimerと衝突回避）</summary>
    private readonly System.Threading.Timer _flushTimer;

    /// <summary>フラッシュ実行中フラグ（同時実行を防ぐ）</summary>
    private int _flushing;

    /// <summary>現在のフィルタ条件のキャッシュ（フィルタ変更時のみ再生成、OnEntryAdded毎の生成を回避）</summary>
    private LogFilter _cachedFilter;

    /// <summary>現在選択中のログエントリ（バッキングフィールド）</summary>
    private LogEntry? _selectedEntry;

    /// <summary>Debugログ表示フラグ（バッキングフィールド）</summary>
    private bool _showDebug = true;

    /// <summary>Infoログ表示フラグ（バッキングフィールド）</summary>
    private bool _showInfo = true;

    /// <summary>Warningログ表示フラグ（バッキングフィールド）</summary>
    private bool _showWarning = true;

    /// <summary>Errorログ表示フラグ（バッキングフィールド）</summary>
    private bool _showError = true;

    /// <summary>検索テキスト（バッキングフィールド）</summary>
    private string _searchText = string.Empty;

    /// <summary>Debugログ件数（バッキングフィールド）</summary>
    private int _debugCount;

    /// <summary>Infoログ件数（バッキングフィールド）</summary>
    private int _infoCount;

    /// <summary>Warningログ件数（バッキングフィールド）</summary>
    private int _warningCount;

    /// <summary>Errorログ件数（バッキングフィールド）</summary>
    private int _errorCount;

    /// <summary>表示用ログエントリ一覧のバッキングフィールド</summary>
    private ObservableCollection<LogEntry> _displayEntries = new();

    /// <summary>
    /// フィルタ適用後の表示用ログエントリ一覧。
    /// UIのリストビューにバインドされる。
    /// RefreshFilter時はコレクション丸ごと差し替えで単一PropertyChanged通知に最適化。
    /// </summary>
    public ObservableCollection<LogEntry> DisplayEntries
    {
        get => _displayEntries;
        private set => SetProperty(ref _displayEntries, value);
    }

    /// <summary>
    /// リストビューで現在選択中のログエントリ。
    /// 詳細パネルの表示内容と連動する。
    /// </summary>
    public LogEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    /// <summary>
    /// Debugレベルのログを表示するかどうか。
    /// 変更時はフィルタを即座に再適用する。
    /// </summary>
    public bool ShowDebug
    {
        get => _showDebug;
        set { if (SetProperty(ref _showDebug, value)) RefreshFilter(); }
    }

    /// <summary>
    /// Infoレベルのログを表示するかどうか。
    /// 変更時はフィルタを即座に再適用する。
    /// </summary>
    public bool ShowInfo
    {
        get => _showInfo;
        set { if (SetProperty(ref _showInfo, value)) RefreshFilter(); }
    }

    /// <summary>
    /// Warningレベルのログを表示するかどうか。
    /// 変更時はフィルタを即座に再適用する。
    /// </summary>
    public bool ShowWarning
    {
        get => _showWarning;
        set { if (SetProperty(ref _showWarning, value)) RefreshFilter(); }
    }

    /// <summary>
    /// Errorレベルのログを表示するかどうか。
    /// 変更時はフィルタを即座に再適用する。
    /// </summary>
    public bool ShowError
    {
        get => _showError;
        set { if (SetProperty(ref _showError, value)) RefreshFilter(); }
    }

    /// <summary>
    /// ログメッセージの検索テキスト（部分一致・大文字小文字無視）。
    /// 変更時はフィルタを即座に再適用する。
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshFilter(); }
    }

    /// <summary>
    /// Debugレベルのログ件数。
    /// フィルタの有無に関わらずストア全体の件数を反映する。
    /// </summary>
    public int DebugCount { get => _debugCount; private set => SetProperty(ref _debugCount, value); }

    /// <summary>
    /// Infoレベルのログ件数。
    /// フィルタの有無に関わらずストア全体の件数を反映する。
    /// </summary>
    public int InfoCount { get => _infoCount; private set => SetProperty(ref _infoCount, value); }

    /// <summary>
    /// Warningレベルのログ件数。
    /// フィルタの有無に関わらずストア全体の件数を反映する。
    /// </summary>
    public int WarningCount { get => _warningCount; private set => SetProperty(ref _warningCount, value); }

    /// <summary>
    /// Errorレベルのログ件数。
    /// フィルタの有無に関わらずストア全体の件数を反映する。
    /// </summary>
    public int ErrorCount { get => _errorCount; private set => SetProperty(ref _errorCount, value); }

    /// <summary>
    /// 全ログをクリアするコマンド。
    /// ストアと表示リストの両方を同時にクリアする。
    /// </summary>
    public ICommand ClearCommand { get; }

    /// <summary>
    /// <see cref="ConsoleViewModel"/> のインスタンスを生成する
    /// </summary>
    /// <param name="logStore">ログエントリを保持するログストア</param>
    /// <param name="uiThread">UIスレッドへの処理マーシャリングインターフェース</param>
    public ConsoleViewModel(LogStore logStore, IUiThread uiThread)
    {
        _logStore = logStore;
        _uiThread = uiThread;
        _cachedFilter = CreateFilter();
        ClearCommand = new RelayCommand(Clear);

        // 新規ログ追加・既存ログ更新（DuplicateCount 増加など）の両方を購読
        _logStore.EntryAdded += OnEntryAdded;
        _logStore.EntryUpdated += OnEntryUpdated;

        // 既存ログを読み込んで初期表示を構築
        RefreshFilter();

        // バッチフラッシュタイマー開始（16ms ごとに UI スレッドへ反映）
        _flushTimer = new System.Threading.Timer(_ => FlushPending(), null, FlushInterval, FlushInterval);
    }

    /// <summary>
    /// 新しいログエントリが追加された際のイベントハンドラ。
    /// 直接 UI スレッドへ Invoke せず、キューに積んでタイマーでバッチ反映する。
    /// </summary>
    /// <param name="sender">イベント発生元（<see cref="LogStore"/>）</param>
    /// <param name="entry">追加されたログエントリ</param>
    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        // ロックフリーキューにエントリを積んで即座にリターン（ログ呼び出し元をブロックしない）
        _pendingAdds.Enqueue(entry);
    }

    /// <summary>
    /// 既存ログエントリが更新（重複折りたたみで DuplicateCount が増加など）された際のイベントハンドラ。
    /// </summary>
    /// <param name="sender">イベント発生元（<see cref="LogStore"/>）</param>
    /// <param name="entry">更新後のログエントリ</param>
    private void OnEntryUpdated(object? sender, LogEntry entry)
    {
        // 更新もキューに積んで一括処理（Id 一致で置換）
        _pendingUpdates.Enqueue(entry);
    }

    /// <summary>
    /// 蓄積された pending 追加・更新を UI スレッドにまとめて反映する。
    /// 同時実行は <see cref="_flushing"/> CAS で 1 つに制限する。
    /// </summary>
    private void FlushPending()
    {
        // 同時実行ガード（フラッシュが詰まった場合にコールバックが重ならないようにする）
        if (System.Threading.Interlocked.CompareExchange(ref _flushing, 1, 0) != 0) return;

        try
        {
            // キューが両方空なら早期リターンして無駄な UI ディスパッチを避ける
            if (_pendingAdds.IsEmpty && _pendingUpdates.IsEmpty) return;

            // 一旦ローカルにドレインしてから UI へ Post する
            var adds = new List<LogEntry>();
            while (_pendingAdds.TryDequeue(out var e)) adds.Add(e);

            var updates = new List<LogEntry>();
            while (_pendingUpdates.TryDequeue(out var e)) updates.Add(e);

            if (adds.Count == 0 && updates.Count == 0) return;

            _uiThread.Invoke(() =>
            {
                // 追加分をまとめてカウント＆フィルタ適用
                foreach (var entry in adds)
                {
                    UpdateCount(entry.Level, 1);
                    if (_cachedFilter.Matches(entry))
                        _displayEntries.Add(entry);
                }

                // 更新分は DisplayEntries 内の同一 Id を置換（線形探索だがバッチサイズは限定的）
                if (updates.Count > 0)
                {
                    // 更新エントリの最新版を Id でまとめる（複数回更新された場合は最後の値を採用）
                    var latest = new Dictionary<int, LogEntry>(updates.Count);
                    foreach (var u in updates) latest[u.Id] = u;

                    for (int i = 0; i < _displayEntries.Count; i++)
                    {
                        if (latest.TryGetValue(_displayEntries[i].Id, out var updated))
                        {
                            // ObservableCollection のインデクサ代入で Replace イベントを発火（UI が更新を検知できる）
                            _displayEntries[i] = updated;
                        }
                    }
                }

                // LogStore 側の循環バッファから追い出された分を表示リストからも切り捨てる。
                // これを行わないと DisplayEntries だけが無制限に伸び、MaxLogEntries の設定が
                // 表示側に効かなくなる（フィルタ操作をしない長時間セッションでメモリを食い続ける）。
                TrimDisplayEntries();
            });
        }
        catch (Exception)
        {
            // System.Threading.Timer のコールバック内で発生した未処理例外はプロセスをクラッシュさせるため、
            // ProfilerEngine.OnTick と同じ規約でここで必ずキャッチする (#33)。
            // UI ディスパッチャがシャットダウン済みの場合や、UI 側の CollectionChanged ハンドラが
            // 例外を投げた場合に到達する。ホスト側に逆流させない。
        }
        finally
        {
            // ガード解除
            System.Threading.Interlocked.Exchange(ref _flushing, 0);
        }
    }

    /// <summary>
    /// 表示リストを <see cref="LogStore.MaxEntries"/> 件以内に収める（先頭＝最古から削除）。
    /// UI スレッド上から呼ぶこと。
    /// </summary>
    private void TrimDisplayEntries()
    {
        var max = _logStore.MaxEntries;
        while (_displayEntries.Count > max)
            _displayEntries.RemoveAt(0);
    }

    /// <summary>
    /// 現在のフィルタ条件で表示リストを全件再構築する。
    /// フィルタ設定変更時に呼び出される。
    /// </summary>
    private void RefreshFilter()
    {
        // フィルタキャッシュを更新（OnEntryAdded でも使われる）
        _cachedFilter = CreateFilter();
        // ストアからフィルタ適用済みエントリを取得
        var entries = _logStore.GetFiltered(_cachedFilter);

        // コレクション丸ごと差し替えで単一 PropertyChanged 通知に最適化
        // （Clear + n回Add の n+1 通知 → 1回の PropertyChanged に削減）
        DisplayEntries = new ObservableCollection<LogEntry>(entries);

        // レベル別カウントをストアの最新値に同期
        var counts = _logStore.GetCounts();
        DebugCount = counts.Debug;
        InfoCount = counts.Info;
        WarningCount = counts.Warning;
        ErrorCount = counts.Error;
    }

    /// <summary>
    /// ログストアと表示リストをすべてクリアし、カウントをリセットする
    /// </summary>
    private void Clear()
    {
        // ストア本体をクリア
        _logStore.Clear();
        // 表示リストもクリア
        DisplayEntries.Clear();
        // バッチング中のエントリも捨てる（クリア後に古いログが混入しないように）
        while (_pendingAdds.TryDequeue(out _)) { }
        while (_pendingUpdates.TryDequeue(out _)) { }
        // 各レベルのカウントをゼロにリセット
        DebugCount = 0;
        InfoCount = 0;
        WarningCount = 0;
        ErrorCount = 0;
        // 選択状態も解除
        SelectedEntry = null;
    }

    /// <summary>
    /// 指定レベルのカウントを指定デルタ値だけ増減させるヘルパー
    /// </summary>
    /// <param name="level">更新対象のログレベル</param>
    /// <param name="delta">増減量（通常は +1 または -1）</param>
    private void UpdateCount(CRLogLevel level, int delta)
    {
        switch (level)
        {
            case CRLogLevel.Debug:   DebugCount   += delta; break;
            case CRLogLevel.Info:    InfoCount    += delta; break;
            case CRLogLevel.Warning: WarningCount += delta; break;
            case CRLogLevel.Error:   ErrorCount   += delta; break;
        }
    }

    /// <summary>
    /// 現在のフィルタプロパティ（各レベルの表示フラグと検索テキスト）から
    /// <see cref="LogFilter"/> オブジェクトを生成して返す
    /// </summary>
    /// <returns>現在設定に対応した <see cref="LogFilter"/> インスタンス</returns>
    private LogFilter CreateFilter() => new(ShowDebug, ShowInfo, ShowWarning, ShowError,
        string.IsNullOrEmpty(SearchText) ? null : SearchText);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // タイマー停止＆解放（バッチフラッシュのコールバック停止）
            try { _flushTimer.Dispose(); } catch { /* 解放経路で握りつぶし */ }

            // LogStore のイベント購読を解除して GC ルートから切る
            _logStore.EntryAdded -= OnEntryAdded;
            _logStore.EntryUpdated -= OnEntryUpdated;
        }

        base.Dispose(disposing);
    }
}
