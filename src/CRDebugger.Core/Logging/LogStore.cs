namespace CRDebugger.Core.Logging;

/// <summary>
/// スレッドセーフなログストア。<see cref="CircularBuffer{T}"/> で容量制限付きのログを管理する。
/// 連続する同一ログを折りたたむ重複抑制機能を備える。
/// </summary>
public sealed class LogStore
{
    /// <summary>ログエントリを格納する循環バッファ</summary>
    private readonly CircularBuffer<LogEntry> _buffer;
    /// <summary>バッファへのスレッドセーフなアクセスを制御する読み書きロック</summary>
    private readonly ReaderWriterLockSlim _lock = new();
    /// <summary>連続する同一ログを折りたたむかどうか</summary>
    private readonly bool _collapseDuplicates;
    /// <summary>ログエントリに付与する連番カウンタ（インターロック操作で更新）</summary>
    private int _nextId;
    /// <summary>直前に記録したログエントリ（重複検出に使用）</summary>
    private LogEntry? _lastEntry;
    /// <summary>Debug レベルログの累積件数（書き込みロック保持中に更新）</summary>
    private int _debugCount;
    /// <summary>Info レベルログの累積件数（書き込みロック保持中に更新）</summary>
    private int _infoCount;
    /// <summary>Warning レベルログの累積件数（書き込みロック保持中に更新）</summary>
    private int _warnCount;
    /// <summary>Error レベルログの累積件数（書き込みロック保持中に更新）</summary>
    private int _errorCount;

    /// <summary>新しいログエントリが追加された時に発火する</summary>
    public event EventHandler<LogEntry>? EntryAdded;
    /// <summary>重複ログが更新された時に発火する（折りたたみ機能使用時）</summary>
    public event EventHandler<LogEntry>? EntryUpdated;

    /// <summary>
    /// <see cref="LogStore"/> のインスタンスを生成する
    /// </summary>
    /// <param name="maxEntries">ログバッファの最大保持件数（デフォルト 2000）</param>
    /// <param name="collapseDuplicates">連続する同一ログを折りたたむか（デフォルト <c>true</c>）</param>
    public LogStore(int maxEntries = 2000, bool collapseDuplicates = true)
    {
        _buffer = new CircularBuffer<LogEntry>(maxEntries);
        _collapseDuplicates = collapseDuplicates;
    }

    /// <summary>
    /// 現在のログエントリ数（読み取りロックを取得して返す）
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try { return _buffer.Count; }
            finally { _lock.ExitReadLock(); }
        }
    }

    /// <summary>
    /// ログエントリを追加する。
    /// 直前エントリと同一内容の場合は重複カウントをインクリメントして更新する（折りたたみ有効時）。
    /// </summary>
    /// <param name="level">ログレベル</param>
    /// <param name="channel">チャネル名（カテゴリ）</param>
    /// <param name="message">ログメッセージ</param>
    /// <param name="stackTrace">スタックトレース（省略可）</param>
    /// <param name="richSpans">リッチテキスト装飾情報（省略可）</param>
    public void Append(CRLogLevel level, string channel, string message,
        string? stackTrace = null, IReadOnlyList<RichTextSpan>? richSpans = null)
    {
        // ロック外でイベント発火するため、ロック内で生成したエントリをキャプチャする変数
        LogEntry? addedEntry = null;

        _lock.EnterWriteLock();
        try
        {
            // 重複ログ折りたたみ: 直前エントリと Level・Channel・Message が一致するか確認する
            if (_collapseDuplicates && _lastEntry != null &&
                _lastEntry.Level == level &&
                _lastEntry.Channel == channel &&
                _lastEntry.Message == message)
            {
                var updated = _lastEntry with
                {
                    DuplicateCount = _lastEntry.DuplicateCount + 1,
                    Timestamp = DateTimeOffset.Now
                };

                _buffer.UpdateLast(updated);
                _lastEntry = updated;

                // デッドロック回避のためロック解放後にイベントを発火する
                _lock.ExitWriteLock();
                try { EntryUpdated?.Invoke(this, updated); }
                catch { /* イベントハンドラの例外でログ記録が失敗しないようにする */ }
                return;
            }

            var entry = new LogEntry(
                Id: Interlocked.Increment(ref _nextId),
                Timestamp: DateTimeOffset.Now,
                Level: level,
                Channel: channel,
                Message: message,
                StackTrace: stackTrace,
                RichSpans: richSpans
            );

            // 満杯時は最古エントリが追い出されるため、該当レベルカウントを先にデクリメントする
            if (_buffer.Count == _buffer.Capacity)
                DecrementLevelCount(_buffer[0].Level);

            _buffer.Add(entry);
            _lastEntry = entry;
            addedEntry = entry; // ロック内でキャプチャ（レースコンディション回避）

            // 追加したエントリのレベル別カウントをインクリメント
            IncrementLevelCount(level);
        }
        finally
        {
            if (_lock.IsWriteLockHeld)
                _lock.ExitWriteLock();
        }

        // ロック外で EntryAdded イベントを発火する
        try
        {
            if (addedEntry != null)
                EntryAdded?.Invoke(this, addedEntry);
        }
        catch
        {
            // イベントハンドラの例外でログ記録が失敗しないようにする
        }
    }

    /// <summary>
    /// 全ログエントリを取得する
    /// </summary>
    /// <returns>全ログエントリのリスト（追加順）</returns>
    public IReadOnlyList<LogEntry> GetAll()
    {
        _lock.EnterReadLock();
        try { return _buffer.ToList(); }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// フィルタ条件に合致するログエントリを取得する
    /// </summary>
    /// <param name="filter">フィルタ条件</param>
    /// <returns>フィルタ条件に合致するログエントリのリスト</returns>
    public IReadOnlyList<LogEntry> GetFiltered(LogFilter filter)
    {
        _lock.EnterReadLock();
        try
        {
            // バッファサイズを上限容量として事前確保し、再アロケーションを回避する
            var result = new List<LogEntry>(_buffer.Count);
            // バッファを順に走査してフィルタに合致するものだけ収集する
            foreach (var entry in _buffer)
            {
                if (filter.Matches(entry))
                    result.Add(entry);
            }
            return result;
        }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// レベル別のログ件数を O(1) で取得する。
    /// インクリメンタルに維持した内部カウンタを返すため、バッファ全体を走査しない。
    /// </summary>
    /// <returns>Debug / Info / Warning / Error それぞれの件数をタプルで返す</returns>
    public (int Debug, int Info, int Warning, int Error) GetCounts()
    {
        _lock.EnterReadLock();
        try
        {
            // 4 つのカウンタを一貫したスナップショットで読み出す
            return (_debugCount, _infoCount, _warnCount, _errorCount);
        }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// 全ログエントリをクリアして初期状態に戻す
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _buffer.Clear();
            // 重複検出用の直前エントリもリセットする
            _lastEntry = null;
            // レベル別カウンタもゼロリセット
            _debugCount = 0;
            _infoCount = 0;
            _warnCount = 0;
            _errorCount = 0;
        }
        finally { _lock.ExitWriteLock(); }
    }

    /// <summary>
    /// 指定レベルのカウンタを 1 増やす（書き込みロック保持中に呼ぶ前提）。
    /// </summary>
    private void IncrementLevelCount(CRLogLevel level)
    {
        switch (level)
        {
            case CRLogLevel.Debug: _debugCount++; break;
            case CRLogLevel.Info: _infoCount++; break;
            case CRLogLevel.Warning: _warnCount++; break;
            case CRLogLevel.Error: _errorCount++; break;
        }
    }

    /// <summary>
    /// 指定レベルのカウンタを 1 減らす（書き込みロック保持中に呼ぶ前提）。
    /// </summary>
    private void DecrementLevelCount(CRLogLevel level)
    {
        switch (level)
        {
            case CRLogLevel.Debug: _debugCount--; break;
            case CRLogLevel.Info: _infoCount--; break;
            case CRLogLevel.Warning: _warnCount--; break;
            case CRLogLevel.Error: _errorCount--; break;
        }
    }
}
