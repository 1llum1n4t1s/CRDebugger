namespace CRDebugger.Core.Logging;

/// <summary>
/// スレッドセーフなログストア。CircularBufferで容量制限付き。
/// </summary>
public sealed class LogStore
{
    private readonly CircularBuffer<LogEntry> _buffer;
    private readonly ReaderWriterLockSlim _lock = new();
    private int _nextId;

    public event EventHandler<LogEntry>? EntryAdded;

    public LogStore(int maxEntries = 2000)
    {
        _buffer = new CircularBuffer<LogEntry>(maxEntries);
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

    public void Append(CRLogLevel level, string channel, string message, string? stackTrace = null)
    {
        var entry = new LogEntry(
            Id: Interlocked.Increment(ref _nextId),
            Timestamp: DateTimeOffset.Now,
            Level: level,
            Channel: channel,
            Message: message,
            StackTrace: stackTrace
        );

        _lock.EnterWriteLock();
        try { _buffer.Add(entry); }
        finally { _lock.ExitWriteLock(); }

        EntryAdded?.Invoke(this, entry);
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
        try { _buffer.Clear(); }
        finally { _lock.ExitWriteLock(); }
    }
}
