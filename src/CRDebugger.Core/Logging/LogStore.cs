namespace CRDebugger.Core.Logging;

/// <summary>
/// スレッドセーフなログストア。CircularBufferで容量制限付き。
/// </summary>
public sealed class LogStore
{
    private readonly CircularBuffer<LogEntry> _buffer;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly bool _collapseDuplicates;
    private int _nextId;
    private LogEntry? _lastEntry;

    public event EventHandler<LogEntry>? EntryAdded;
    /// <summary>重複ログが更新された時に発火（折りたたみ時）</summary>
    public event EventHandler<LogEntry>? EntryUpdated;

    public LogStore(int maxEntries = 2000, bool collapseDuplicates = true)
    {
        _buffer = new CircularBuffer<LogEntry>(maxEntries);
        _collapseDuplicates = collapseDuplicates;
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try { return _buffer.Count; }
            finally { _lock.ExitReadLock(); }
        }
    }

    public void Append(CRLogLevel level, string channel, string message,
        string? stackTrace = null, IReadOnlyList<RichTextSpan>? richSpans = null)
    {
        _lock.EnterWriteLock();
        try
        {
            // 重複ログ折りたたみ
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

                // バッファ内の最後のエントリを更新
                _buffer.UpdateLast(updated);
                _lastEntry = updated;

                // ロック外でイベント発火
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

            _buffer.Add(entry);
            _lastEntry = entry;
        }
        finally
        {
            if (_lock.IsWriteLockHeld)
                _lock.ExitWriteLock();
        }

        try
        {
            EntryAdded?.Invoke(this, _lastEntry!);
        }
        catch
        {
            // イベントハンドラの例外でログ記録が失敗しないようにする
        }
    }

    public IReadOnlyList<LogEntry> GetAll()
    {
        _lock.EnterReadLock();
        try { return _buffer.ToList(); }
        finally { _lock.ExitReadLock(); }
    }

    public IReadOnlyList<LogEntry> GetFiltered(LogFilter filter)
    {
        _lock.EnterReadLock();
        try
        {
            var result = new List<LogEntry>();
            foreach (var entry in _buffer)
            {
                if (filter.Matches(entry))
                    result.Add(entry);
            }
            return result;
        }
        finally { _lock.ExitReadLock(); }
    }

    public (int Debug, int Info, int Warning, int Error) GetCounts()
    {
        int d = 0, i = 0, w = 0, e = 0;
        _lock.EnterReadLock();
        try
        {
            foreach (var entry in _buffer)
            {
                switch (entry.Level)
                {
                    case CRLogLevel.Debug: d++; break;
                    case CRLogLevel.Info: i++; break;
                    case CRLogLevel.Warning: w++; break;
                    case CRLogLevel.Error: e++; break;
                }
            }
        }
        finally { _lock.ExitReadLock(); }
        return (d, i, w, e);
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _buffer.Clear();
            _lastEntry = null;
        }
        finally { _lock.ExitWriteLock(); }
    }
}
