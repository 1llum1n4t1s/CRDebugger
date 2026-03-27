using System.Collections.ObjectModel;
using System.Windows.Input;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Logging;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// コンソール（ログ）タブのViewModel。
/// ログエントリの表示・フィルタリング・検索機能を提供する。
/// ログストアの変更イベントを購読し、UIをリアルタイムで更新する。
/// </summary>
public sealed class ConsoleViewModel : ViewModelBase
{
    /// <summary>ログストアへの参照（ログデータのソース）</summary>
    private readonly LogStore _logStore;

    /// <summary>UIスレッドマーシャリング用インターフェース</summary>
    private readonly IUiThread _uiThread;

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

    /// <summary>
    /// フィルタ適用後の表示用ログエントリ一覧。
    /// UIのリストビューにバインドされる。
    /// </summary>
    public ObservableCollection<LogEntry> DisplayEntries { get; } = new();

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
        ClearCommand = new RelayCommand(Clear);

        // 新規ログ追加時にUIを更新するイベントハンドラを登録
        _logStore.EntryAdded += OnEntryAdded;

        // 既存ログを読み込んで初期表示を構築
        RefreshFilter();
    }

    /// <summary>
    /// 新しいログエントリが追加された際のイベントハンドラ。
    /// UIスレッド上でカウント更新とフィルタ適用を行う。
    /// </summary>
    /// <param name="sender">イベント発生元（<see cref="LogStore"/>）</param>
    /// <param name="entry">追加されたログエントリ</param>
    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        // UIスレッド外からの呼び出しを安全にUIスレッドへマーシャリング
        _uiThread.Invoke(() =>
        {
            // レベル別カウントをインクリメント
            UpdateCount(entry.Level, 1);

            // フィルタ条件に合致するエントリのみ表示リストへ追加
            var filter = CreateFilter();
            if (filter.Matches(entry))
            {
                DisplayEntries.Add(entry);
            }
        });
    }

    /// <summary>
    /// 現在のフィルタ条件で表示リストを全件再構築する。
    /// フィルタ設定変更時に呼び出される。
    /// </summary>
    private void RefreshFilter()
    {
        // 表示リストをいったんクリアして再構築
        DisplayEntries.Clear();
        var filter = CreateFilter();
        // ストアからフィルタ適用済みエントリを取得して表示リストに追加
        var entries = _logStore.GetFiltered(filter);
        foreach (var entry in entries)
            DisplayEntries.Add(entry);

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
}
