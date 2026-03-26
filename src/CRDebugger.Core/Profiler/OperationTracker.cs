using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CRDebugger.Core.Profiler;

/// <summary>
/// ロジック単位のプロファイリングを管理するトラッカー
/// </summary>
public sealed class OperationTracker
{
    private readonly ConcurrentDictionary<string, OperationMetrics> _metrics = new();
    private long _manualNetworkRead;
    private long _manualNetworkWrite;
    private long _manualStorageRead;
    private long _manualStorageWrite;

    /// <summary>メトリクスが更新された時に発火</summary>
    public event EventHandler<OperationMetrics>? MetricsUpdated;

    /// <summary>
    /// 計測スコープを開始する（usingパターン）
    /// </summary>
    public ProfilingScope BeginScope(string operationName, string category = "General")
    {
        return new ProfilingScope(this, operationName, category);
    }

    /// <summary>
    /// 同期処理を計測する
    /// </summary>
    public T Measure<T>(string operationName, Func<T> action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        return action();
    }

    /// <summary>
    /// 同期処理を計測する（戻り値なし）
    /// </summary>
    public void Measure(string operationName, Action action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        action();
    }

    /// <summary>
    /// 非同期処理を計測する
    /// </summary>
    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        return await action().ConfigureAwait(false);
    }

    /// <summary>
    /// 非同期処理を計測する（戻り値なし）
    /// </summary>
    public async Task MeasureAsync(string operationName, Func<Task> action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        await action().ConfigureAwait(false);
    }

    /// <summary>
    /// ネットワークI/Oを手動で記録する
    /// </summary>
    public void RecordNetworkIO(long bytesRead, long bytesWritten)
    {
        Interlocked.Add(ref _manualNetworkRead, bytesRead);
        Interlocked.Add(ref _manualNetworkWrite, bytesWritten);
    }

    /// <summary>
    /// ストレージI/Oを手動で記録する
    /// </summary>
    public void RecordStorageIO(long bytesRead, long bytesWritten)
    {
        Interlocked.Add(ref _manualStorageRead, bytesRead);
        Interlocked.Add(ref _manualStorageWrite, bytesWritten);
    }

    /// <summary>現在のネットワークカウンターを取得</summary>
    internal (long Read, long Write) GetNetworkCounters()
    {
        try
        {
            // OSレベルのネットワーク統計 + 手動記録
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            long totalRead = 0, totalWrite = 0;
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var stats = ni.GetIPStatistics();
                totalRead += stats.BytesReceived;
                totalWrite += stats.BytesSent;
            }
            return (
                totalRead + Interlocked.Read(ref _manualNetworkRead),
                totalWrite + Interlocked.Read(ref _manualNetworkWrite)
            );
        }
        catch
        {
            return (
                Interlocked.Read(ref _manualNetworkRead),
                Interlocked.Read(ref _manualNetworkWrite)
            );
        }
    }

    /// <summary>現在のストレージカウンターを取得</summary>
    internal (long Read, long Write) GetStorageCounters()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return (
                process.WorkingSet64 + Interlocked.Read(ref _manualStorageRead),  // 近似値
                Interlocked.Read(ref _manualStorageWrite)
            );
        }
        catch
        {
            return (
                Interlocked.Read(ref _manualStorageRead),
                Interlocked.Read(ref _manualStorageWrite)
            );
        }
    }

    /// <summary>サンプルを記録（ProfilingScopeから呼ばれる）</summary>
    internal void RecordSample(string operationName, string category, OperationSample sample)
    {
        var metrics = _metrics.GetOrAdd(operationName, name => new OperationMetrics(name, category));
        metrics.RecordSample(sample);

        try { MetricsUpdated?.Invoke(this, metrics); }
        catch { /* イベントハンドラの例外を握りつぶす */ }
    }

    /// <summary>全操作のメトリクス一覧</summary>
    public IReadOnlyList<OperationMetrics> GetAllMetrics()
    {
        return _metrics.Values.OrderByDescending(m => m.TotalDuration).ToList();
    }

    /// <summary>カテゴリ別のメトリクス一覧</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OperationMetrics>> GetMetricsByCategory()
    {
        return _metrics.Values
            .GroupBy(m => m.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<OperationMetrics>)g.OrderByDescending(m => m.TotalDuration).ToList()
            );
    }

    /// <summary>指定操作のメトリクスを取得</summary>
    public OperationMetrics? GetMetrics(string operationName)
    {
        return _metrics.TryGetValue(operationName, out var metrics) ? metrics : null;
    }

    /// <summary>処理時間のホットスポット上位N件</summary>
    public IReadOnlyList<OperationMetrics> GetDurationHotspots(int topN = 10)
    {
        return _metrics.Values.OrderByDescending(m => m.TotalDuration).Take(topN).ToList();
    }

    /// <summary>CPU時間のホットスポット上位N件</summary>
    public IReadOnlyList<OperationMetrics> GetCpuHotspots(int topN = 10)
    {
        return _metrics.Values.OrderByDescending(m => m.TotalCpuTime).Take(topN).ToList();
    }

    /// <summary>メモリ消費のホットスポット上位N件</summary>
    public IReadOnlyList<OperationMetrics> GetMemoryHotspots(int topN = 10)
    {
        return _metrics.Values.OrderByDescending(m => m.TotalMemoryDelta).Take(topN).ToList();
    }

    /// <summary>ネットワークI/Oのホットスポット上位N件</summary>
    public IReadOnlyList<OperationMetrics> GetNetworkHotspots(int topN = 10)
    {
        return _metrics.Values
            .OrderByDescending(m => m.TotalNetworkBytesRead + m.TotalNetworkBytesWritten)
            .Take(topN).ToList();
    }

    /// <summary>ストレージI/Oのホットスポット上位N件</summary>
    public IReadOnlyList<OperationMetrics> GetStorageHotspots(int topN = 10)
    {
        return _metrics.Values
            .OrderByDescending(m => m.TotalStorageBytesRead + m.TotalStorageBytesWritten)
            .Take(topN).ToList();
    }

    /// <summary>全メトリクスをリセット</summary>
    public void ResetAll()
    {
        foreach (var metrics in _metrics.Values)
            metrics.Reset();
    }

    /// <summary>全メトリクスをクリア</summary>
    public void Clear()
    {
        _metrics.Clear();
        Interlocked.Exchange(ref _manualNetworkRead, 0);
        Interlocked.Exchange(ref _manualNetworkWrite, 0);
        Interlocked.Exchange(ref _manualStorageRead, 0);
        Interlocked.Exchange(ref _manualStorageWrite, 0);
    }
}
