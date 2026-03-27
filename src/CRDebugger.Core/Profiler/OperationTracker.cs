using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CRDebugger.Core.Profiler;

/// <summary>
/// ロジック単位のプロファイリングを一元管理するトラッカー。
/// BeginScope / Measure / MeasureAsync で計測スコープを開始し、
/// 結果を <see cref="OperationMetrics"/> に集計する。
/// </summary>
public sealed class OperationTracker
{
    /// <summary>操作名をキーとしてメトリクスを保持するスレッドセーフな辞書</summary>
    private readonly ConcurrentDictionary<string, OperationMetrics> _metrics = new();

    /// <summary>手動記録されたネットワーク受信バイト数の累計（Interlocked で操作）</summary>
    private long _manualNetworkRead;

    /// <summary>手動記録されたネットワーク送信バイト数の累計（Interlocked で操作）</summary>
    private long _manualNetworkWrite;

    /// <summary>手動記録されたストレージ読み込みバイト数の累計（Interlocked で操作）</summary>
    private long _manualStorageRead;

    /// <summary>手動記録されたストレージ書き込みバイト数の累計（Interlocked で操作）</summary>
    private long _manualStorageWrite;

    /// <summary>
    /// いずれかの操作のメトリクスが更新された時に発火するイベント。
    /// 引数には更新された <see cref="OperationMetrics"/> インスタンスが渡される。
    /// </summary>
    public event EventHandler<OperationMetrics>? MetricsUpdated;

    /// <summary>
    /// 計測スコープを開始する（usingパターン）。
    /// スコープを Dispose した時点で計測が終了し、結果が記録される。
    /// </summary>
    /// <param name="operationName">計測する操作の名前</param>
    /// <param name="category">操作をグループ化するカテゴリタグ（省略時は "General"）</param>
    /// <returns>Dispose 時に計測を完了する <see cref="ProfilingScope"/></returns>
    public ProfilingScope BeginScope(string operationName, string category = "General")
    {
        return new ProfilingScope(this, operationName, category);
    }

    /// <summary>
    /// 同期処理を計測し、戻り値を返す。
    /// 内部で <see cref="BeginScope"/> を使用して計測スコープを管理する。
    /// </summary>
    /// <typeparam name="T">処理の戻り値の型</typeparam>
    /// <param name="operationName">計測する操作の名前</param>
    /// <param name="action">計測対象の同期処理</param>
    /// <param name="category">操作をグループ化するカテゴリタグ（省略時は "General"）</param>
    /// <returns>action の戻り値</returns>
    public T Measure<T>(string operationName, Func<T> action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        return action();
    }

    /// <summary>
    /// 同期処理を計測する（戻り値なし）。
    /// 内部で <see cref="BeginScope"/> を使用して計測スコープを管理する。
    /// </summary>
    /// <param name="operationName">計測する操作の名前</param>
    /// <param name="action">計測対象の同期処理</param>
    /// <param name="category">操作をグループ化するカテゴリタグ（省略時は "General"）</param>
    public void Measure(string operationName, Action action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        action();
    }

    /// <summary>
    /// 非同期処理を計測し、戻り値を返す。
    /// 内部で <see cref="BeginScope"/> を使用して計測スコープを管理する。
    /// </summary>
    /// <typeparam name="T">非同期処理の戻り値の型</typeparam>
    /// <param name="operationName">計測する操作の名前</param>
    /// <param name="action">計測対象の非同期処理</param>
    /// <param name="category">操作をグループ化するカテゴリタグ（省略時は "General"）</param>
    /// <returns>action の戻り値を持つ Task</returns>
    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        return await action().ConfigureAwait(false);
    }

    /// <summary>
    /// 非同期処理を計測する（戻り値なし）。
    /// 内部で <see cref="BeginScope"/> を使用して計測スコープを管理する。
    /// </summary>
    /// <param name="operationName">計測する操作の名前</param>
    /// <param name="action">計測対象の非同期処理</param>
    /// <param name="category">操作をグループ化するカテゴリタグ（省略時は "General"）</param>
    /// <returns>完了を表す Task</returns>
    public async Task MeasureAsync(string operationName, Func<Task> action, string category = "General")
    {
        using var scope = BeginScope(operationName, category);
        await action().ConfigureAwait(false);
    }

    /// <summary>
    /// ネットワークI/Oを手動で記録する。
    /// OSレベルで取得できないネットワーク通信量をアプリ側で計上する際に使用する。
    /// </summary>
    /// <param name="bytesRead">受信バイト数</param>
    /// <param name="bytesWritten">送信バイト数</param>
    public void RecordNetworkIO(long bytesRead, long bytesWritten)
    {
        // Interlocked.Add でスレッドセーフに加算
        Interlocked.Add(ref _manualNetworkRead, bytesRead);
        Interlocked.Add(ref _manualNetworkWrite, bytesWritten);
    }

    /// <summary>
    /// ストレージI/Oを手動で記録する。
    /// OSレベルで取得できないディスクI/O量をアプリ側で計上する際に使用する。
    /// </summary>
    /// <param name="bytesRead">読み込みバイト数</param>
    /// <param name="bytesWritten">書き込みバイト数</param>
    public void RecordStorageIO(long bytesRead, long bytesWritten)
    {
        // Interlocked.Add でスレッドセーフに加算
        Interlocked.Add(ref _manualStorageRead, bytesRead);
        Interlocked.Add(ref _manualStorageWrite, bytesWritten);
    }

    /// <summary>
    /// 現在のネットワークI/Oカウンター値を取得する。
    /// OSレベルの統計値と手動記録値を合算して返す。
    /// </summary>
    /// <returns>（受信バイト数, 送信バイト数）のタプル</returns>
    internal (long Read, long Write) GetNetworkCounters()
    {
        try
        {
            // アクティブな全ネットワークインターフェースのOSレベル統計を集計
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            long totalRead = 0, totalWrite = 0;
            foreach (var ni in interfaces)
            {
                // 稼働中のインターフェースのみを対象にする
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var stats = ni.GetIPStatistics();
                totalRead += stats.BytesReceived;
                totalWrite += stats.BytesSent;
            }
            // OS統計値に手動記録分を加算して返す
            return (
                totalRead + Interlocked.Read(ref _manualNetworkRead),
                totalWrite + Interlocked.Read(ref _manualNetworkWrite)
            );
        }
        catch
        {
            // OS統計の取得に失敗した場合は手動記録分のみを返す
            return (
                Interlocked.Read(ref _manualNetworkRead),
                Interlocked.Read(ref _manualNetworkWrite)
            );
        }
    }

    /// <summary>
    /// 現在のストレージI/Oカウンター値を取得する。
    /// プロセスのワーキングセットを近似値として使用し、手動記録値を加算する。
    /// </summary>
    /// <returns>（読み込みバイト数, 書き込みバイト数）のタプル</returns>
    internal (long Read, long Write) GetStorageCounters()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return (
                process.WorkingSet64 + Interlocked.Read(ref _manualStorageRead),  // ワーキングセットを近似値として使用
                Interlocked.Read(ref _manualStorageWrite)
            );
        }
        catch
        {
            // プロセス情報の取得に失敗した場合は手動記録分のみを返す
            return (
                Interlocked.Read(ref _manualStorageRead),
                Interlocked.Read(ref _manualStorageWrite)
            );
        }
    }

    /// <summary>
    /// 計測サンプルを対応する <see cref="OperationMetrics"/> に記録し、
    /// <see cref="MetricsUpdated"/> イベントを発火する。
    /// <see cref="ProfilingScope"/> から内部的に呼ばれる。
    /// </summary>
    /// <param name="operationName">操作名（メトリクスのキーとして使用）</param>
    /// <param name="category">カテゴリタグ（新規メトリクス作成時に設定）</param>
    /// <param name="sample">記録するサンプルデータ</param>
    internal void RecordSample(string operationName, string category, OperationSample sample)
    {
        // 既存メトリクスを取得、なければ新規作成して辞書に追加
        var metrics = _metrics.GetOrAdd(operationName, name => new OperationMetrics(name, category));
        metrics.RecordSample(sample);

        try { MetricsUpdated?.Invoke(this, metrics); }
        catch { /* イベントハンドラの例外はプロファイリング処理に影響させないため握りつぶす */ }
    }

    /// <summary>
    /// 記録されている全操作のメトリクス一覧を返す。
    /// 合計処理時間の降順にソートされる。
    /// </summary>
    /// <returns>全 <see cref="OperationMetrics"/> の一覧（TotalDuration 降順）</returns>
    public IReadOnlyList<OperationMetrics> GetAllMetrics()
    {
        return _metrics.Values.OrderByDescending(m => m.TotalDuration).ToList();
    }

    /// <summary>
    /// カテゴリ別にグループ化したメトリクス一覧を返す。
    /// 各グループ内は合計処理時間の降順にソートされる。
    /// </summary>
    /// <returns>カテゴリ名をキーとする <see cref="OperationMetrics"/> のリスト辞書</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<OperationMetrics>> GetMetricsByCategory()
    {
        return _metrics.Values
            .GroupBy(m => m.Category)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<OperationMetrics>)g.OrderByDescending(m => m.TotalDuration).ToList()
            );
    }

    /// <summary>
    /// 指定した操作名のメトリクスを取得する。
    /// </summary>
    /// <param name="operationName">取得する操作の名前</param>
    /// <returns>対応する <see cref="OperationMetrics"/>。存在しない場合は <c>null</c></returns>
    public OperationMetrics? GetMetrics(string operationName)
    {
        return _metrics.TryGetValue(operationName, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// 合計処理時間（TotalDuration）が大きい順にホットスポットを返す。
    /// </summary>
    /// <param name="topN">取得件数の上限（省略時は10件）</param>
    /// <returns>処理時間上位 N 件の <see cref="OperationMetrics"/> 一覧</returns>
    public IReadOnlyList<OperationMetrics> GetDurationHotspots(int topN = 10)
    {
        return _metrics.Values.OrderByDescending(m => m.TotalDuration).Take(topN).ToList();
    }

    /// <summary>
    /// 合計CPU時間（TotalCpuTime）が大きい順にホットスポットを返す。
    /// </summary>
    /// <param name="topN">取得件数の上限（省略時は10件）</param>
    /// <returns>CPU時間上位 N 件の <see cref="OperationMetrics"/> 一覧</returns>
    public IReadOnlyList<OperationMetrics> GetCpuHotspots(int topN = 10)
    {
        return _metrics.Values.OrderByDescending(m => m.TotalCpuTime).Take(topN).ToList();
    }

    /// <summary>
    /// 合計メモリ増加量（TotalMemoryDelta）が大きい順にホットスポットを返す。
    /// </summary>
    /// <param name="topN">取得件数の上限（省略時は10件）</param>
    /// <returns>メモリ消費上位 N 件の <see cref="OperationMetrics"/> 一覧</returns>
    public IReadOnlyList<OperationMetrics> GetMemoryHotspots(int topN = 10)
    {
        return _metrics.Values.OrderByDescending(m => m.TotalMemoryDelta).Take(topN).ToList();
    }

    /// <summary>
    /// 合計ネットワークI/O（送受信合計）が大きい順にホットスポットを返す。
    /// </summary>
    /// <param name="topN">取得件数の上限（省略時は10件）</param>
    /// <returns>ネットワークI/O上位 N 件の <see cref="OperationMetrics"/> 一覧</returns>
    public IReadOnlyList<OperationMetrics> GetNetworkHotspots(int topN = 10)
    {
        return _metrics.Values
            .OrderByDescending(m => m.TotalNetworkBytesRead + m.TotalNetworkBytesWritten)
            .Take(topN).ToList();
    }

    /// <summary>
    /// 合計ストレージI/O（読み書き合計）が大きい順にホットスポットを返す。
    /// </summary>
    /// <param name="topN">取得件数の上限（省略時は10件）</param>
    /// <returns>ストレージI/O上位 N 件の <see cref="OperationMetrics"/> 一覧</returns>
    public IReadOnlyList<OperationMetrics> GetStorageHotspots(int topN = 10)
    {
        return _metrics.Values
            .OrderByDescending(m => m.TotalStorageBytesRead + m.TotalStorageBytesWritten)
            .Take(topN).ToList();
    }

    /// <summary>
    /// 全操作のメトリクスをリセットする。
    /// 操作のエントリ自体は残したまま、各メトリクスの集計値を初期化する。
    /// </summary>
    public void ResetAll()
    {
        // 各メトリクスの集計値を個別にリセット
        foreach (var metrics in _metrics.Values)
            metrics.Reset();
    }

    /// <summary>
    /// 全操作のメトリクスエントリを完全に削除し、手動記録のI/Oカウンターもリセットする。
    /// <see cref="ResetAll"/> と異なり、操作名のエントリ自体も消去される。
    /// </summary>
    public void Clear()
    {
        // 操作メトリクス辞書を全消去
        _metrics.Clear();

        // 手動記録のI/Oカウンターをゼロリセット（Interlocked.Exchange でスレッドセーフに実施）
        Interlocked.Exchange(ref _manualNetworkRead, 0);
        Interlocked.Exchange(ref _manualNetworkWrite, 0);
        Interlocked.Exchange(ref _manualStorageRead, 0);
        Interlocked.Exchange(ref _manualStorageWrite, 0);
    }
}
