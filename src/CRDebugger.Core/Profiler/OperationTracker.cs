using System.Collections.Concurrent;

namespace CRDebugger.Core.Profiler;

/// <summary>
/// ロジック単位のプロファイリングを一元管理するトラッカー。
/// BeginScope / Measure / MeasureAsync で計測スコープを開始し、
/// 結果を <see cref="OperationMetrics"/> に集計する。
/// </summary>
public sealed class OperationTracker
{
    /// <summary>
    /// _metrics に保持できる最大操作数。これを超えると最古エントリを LRU 風に追い出す。
    /// 永続リーク（攻撃的に毎回ユニークな操作名を渡されるケース）を防ぐためのガード。
    /// </summary>
    public const int MaxOperations = 1024;

    /// <summary>操作名をキーとしてメトリクスを保持するスレッドセーフな辞書</summary>
    private readonly ConcurrentDictionary<string, OperationMetrics> _metrics = new();

    /// <summary>_metrics に追加された操作名の挿入順を追跡する FIFO キュー（LRU 風 eviction 用）</summary>
    private readonly ConcurrentQueue<string> _orderTracking = new();

    /// <summary>手動記録されたネットワーク受信バイト数の累計（Interlocked で操作）</summary>
    private long _manualNetworkRead;

    /// <summary>手動記録されたネットワーク送信バイト数の累計（Interlocked で操作）</summary>
    private long _manualNetworkWrite;

    /// <summary>手動記録されたストレージ読み込みバイト数の累計（Interlocked で操作）</summary>
    private long _manualStorageRead;

    /// <summary>手動記録されたストレージ書き込みバイト数の累計（Interlocked で操作）</summary>
    private long _manualStorageWrite;

    /// <summary>現在の論理実行コンテキストで有効な I/O 計測スコープ。</summary>
    private readonly AsyncLocal<ScopeIoCounters?> _activeIoScope = new();

    /// <summary>1つの論理スコープへ明示記録された I/O 量。</summary>
    internal sealed class ScopeIoCounters
    {
        internal ScopeIoCounters? Parent { get; }
        private long _networkRead;
        private long _networkWrite;
        private long _storageRead;
        private long _storageWrite;

        internal ScopeIoCounters(ScopeIoCounters? parent) => Parent = parent;

        internal void AddNetwork(long bytesRead, long bytesWritten)
        {
            Interlocked.Add(ref _networkRead, bytesRead);
            Interlocked.Add(ref _networkWrite, bytesWritten);
        }

        internal void AddStorage(long bytesRead, long bytesWritten)
        {
            Interlocked.Add(ref _storageRead, bytesRead);
            Interlocked.Add(ref _storageWrite, bytesWritten);
        }

        internal (long NetworkRead, long NetworkWrite, long StorageRead, long StorageWrite) Snapshot() =>
            (Interlocked.Read(ref _networkRead), Interlocked.Read(ref _networkWrite),
             Interlocked.Read(ref _storageRead), Interlocked.Read(ref _storageWrite));
    }

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
        var ioCounters = new ScopeIoCounters(_activeIoScope.Value);
        _activeIoScope.Value = ioCounters;
        return new ProfilingScope(this, operationName, category, ioCounters);
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
    /// 現在の論理スコープとその親スコープへ帰属し、OS全体の通信量は自動加算しない。
    /// </summary>
    /// <param name="bytesRead">受信バイト数</param>
    /// <param name="bytesWritten">送信バイト数</param>
    public void RecordNetworkIO(long bytesRead, long bytesWritten)
    {
        // 全体の明示記録量を保持しつつ、現在の論理スコープとその親へだけ帰属させる。
        Interlocked.Add(ref _manualNetworkRead, bytesRead);
        Interlocked.Add(ref _manualNetworkWrite, bytesWritten);
        for (var scope = _activeIoScope.Value; scope != null; scope = scope.Parent)
            scope.AddNetwork(bytesRead, bytesWritten);
    }

    /// <summary>
    /// ストレージI/Oを手動で記録する。
    /// 現在の論理スコープとその親スコープへ帰属し、ワーキングセット等は自動加算しない。
    /// </summary>
    /// <param name="bytesRead">読み込みバイト数</param>
    /// <param name="bytesWritten">書き込みバイト数</param>
    public void RecordStorageIO(long bytesRead, long bytesWritten)
    {
        // 全体の明示記録量を保持しつつ、現在の論理スコープとその親へだけ帰属させる。
        Interlocked.Add(ref _manualStorageRead, bytesRead);
        Interlocked.Add(ref _manualStorageWrite, bytesWritten);
        for (var scope = _activeIoScope.Value; scope != null; scope = scope.Parent)
            scope.AddStorage(bytesRead, bytesWritten);
    }

    /// <summary>
    /// 旧バージョンとのソース互換性のために残されたメソッド。
    /// I/O は <see cref="RecordNetworkIO"/> / <see cref="RecordStorageIO"/> で即時反映されるため、
    /// 現在は OS 全体のカウンターやワーキングセットを読み取らず何もしない。
    /// </summary>
    public void UpdateCounterSnapshot()
    {
    }

    /// <summary>
    /// 現在の明示記録済みネットワークI/Oカウンター値を取得する。
    /// </summary>
    /// <returns>（受信バイト数, 送信バイト数）のタプル</returns>
    internal (long Read, long Write) GetNetworkCounters()
    {
        return (
            Interlocked.Read(ref _manualNetworkRead),
            Interlocked.Read(ref _manualNetworkWrite)
        );
    }

    /// <summary>
    /// 現在の明示記録済みストレージI/Oカウンター値を取得する。
    /// </summary>
    /// <returns>（読み込みバイト数, 書き込みバイト数）のタプル</returns>
    internal (long Read, long Write) GetStorageCounters()
    {
        return (
            Interlocked.Read(ref _manualStorageRead),
            Interlocked.Read(ref _manualStorageWrite)
        );
    }

    /// <summary>完了した論理スコープを現在値から外し、親スコープへ戻す。</summary>
    internal void CompleteIoScope(ScopeIoCounters scope)
    {
        if (ReferenceEquals(_activeIoScope.Value, scope))
            _activeIoScope.Value = scope.Parent;
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
        var isNew = false;
        var metrics = _metrics.GetOrAdd(operationName, name =>
        {
            isNew = true;
            return new OperationMetrics(name, category);
        });

        if (isNew)
        {
            // 新規操作名は順序追跡キューに追加し、上限超過時に最古を追い出す
            _orderTracking.Enqueue(operationName);
            EvictIfOverCapacity();
        }

        metrics.RecordSample(sample);

        try { MetricsUpdated?.Invoke(this, metrics); }
        catch { /* イベントハンドラの例外はプロファイリング処理に影響させないため握りつぶす */ }
    }

    /// <summary>
    /// _metrics の件数が <see cref="MaxOperations"/> を超えている場合、
    /// 最古エントリを <see cref="_orderTracking"/> から順番に取り出して削除する (#5)。
    /// 攻撃的に毎回ユニークな操作名を渡されても _metrics が無限に膨張しないようにする。
    /// </summary>
    private void EvictIfOverCapacity()
    {
        // 上限を超過している間ループ（GetOrAdd と並行追加で多重超過する可能性に備える）
        while (_metrics.Count > MaxOperations && _orderTracking.TryDequeue(out var oldest))
        {
            // キューの先頭が現存していれば削除する。既に削除済みなら次のエントリを処理
            _metrics.TryRemove(oldest, out _);
        }
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

        // 順序追跡キューもまとめて空にする（同等の効果を得るため Dequeue ループで消費）
        while (_orderTracking.TryDequeue(out _)) { }

        // 手動記録のI/Oカウンターをゼロリセット（Interlocked.Exchange でスレッドセーフに実施）
        Interlocked.Exchange(ref _manualNetworkRead, 0);
        Interlocked.Exchange(ref _manualNetworkWrite, 0);
        Interlocked.Exchange(ref _manualStorageRead, 0);
        Interlocked.Exchange(ref _manualStorageWrite, 0);

    }
}
