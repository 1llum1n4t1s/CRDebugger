using System.Diagnostics;

namespace CRDebugger.Core.Profiler;

/// <summary>
/// usingパターンで操作の計測スコープを管理するクラス。
/// コンストラクタで各メトリクスの開始値をキャプチャし、
/// <see cref="Dispose"/> 時に終了値と差分を計算して <see cref="OperationTracker"/> に記録する。
/// 使用例:
/// <code>
/// using (CRDebugger.Profile("データベースクエリ"))
/// {
///     await db.QueryAsync(...);
/// }
/// </code>
/// </summary>
public sealed class ProfilingScope : IDisposable
{
    /// <summary>計測結果を記録する先のトラッカー</summary>
    private readonly OperationTracker _tracker;

    /// <summary>この計測スコープが対象とする操作の名前</summary>
    private readonly string _operationName;

    /// <summary>この計測スコープが属するカテゴリタグ</summary>
    private readonly string _category;

    /// <summary>処理時間（ウォールクロック時間）の計測に使用するストップウォッチ</summary>
    private readonly Stopwatch _stopwatch;

    /// <summary>スコープ開始時点のCPU時間</summary>
    private readonly TimeSpan _startCpuTime;

    /// <summary>スコープ開始時点のGC管理メモリ量（バイト）</summary>
    private readonly long _startMemory;

    /// <summary>スコープ開始時点のネットワーク受信バイト数</summary>
    private readonly long _startNetworkRead;

    /// <summary>スコープ開始時点のネットワーク送信バイト数</summary>
    private readonly long _startNetworkWrite;

    /// <summary>スコープ開始時点のストレージ読み込みバイト数</summary>
    private readonly long _startStorageRead;

    /// <summary>スコープ開始時点のストレージ書き込みバイト数</summary>
    private readonly long _startStorageWrite;

    /// <summary><see cref="Dispose"/> が既に呼ばれたかどうかを示すフラグ（二重記録防止用）</summary>
    private bool _disposed;

    /// <summary>
    /// 計測スコープを初期化し、各メトリクスの開始値を記録する。
    /// このコンストラクタは <see cref="OperationTracker.BeginScope"/> からのみ呼ばれる。
    /// </summary>
    /// <param name="tracker">計測結果の記録先トラッカー</param>
    /// <param name="operationName">計測する操作の名前</param>
    /// <param name="category">操作のカテゴリタグ</param>
    internal ProfilingScope(OperationTracker tracker, string operationName, string category)
    {
        _tracker = tracker;
        _operationName = operationName;
        _category = category;

        // 開始時点の各メトリクス値をキャプチャ（終了時の差分計算に使用）
        _startCpuTime = GetProcessCpuTime();
        _startMemory = GC.GetTotalMemory(false);
        (_startNetworkRead, _startNetworkWrite) = tracker.GetNetworkCounters();
        (_startStorageRead, _startStorageWrite) = tracker.GetStorageCounters();

        // ウォールクロック計測を開始
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// 計測を終了し、開始時からの差分を <see cref="OperationSample"/> にまとめてトラッカーに記録する。
    /// 二重呼び出しを防ぐため、初回呼び出し以降は何もしない。
    /// </summary>
    public void Dispose()
    {
        // 二重Dispose を防ぐ（usingとexplicitのDispose両方が呼ばれるケースに対応）
        if (_disposed) return;
        _disposed = true;

        // ウォールクロック計測を停止
        _stopwatch.Stop();

        // 終了時点の各メトリクス値を取得
        var endCpuTime = GetProcessCpuTime();
        var endMemory = GC.GetTotalMemory(false);
        var (endNetRead, endNetWrite) = _tracker.GetNetworkCounters();
        var (endStoreRead, endStoreWrite) = _tracker.GetStorageCounters();

        // 開始時との差分を計算してサンプルレコードを生成
        var sample = new OperationSample(
            Timestamp: DateTimeOffset.Now,
            Duration: _stopwatch.Elapsed,
            CpuTime: endCpuTime - _startCpuTime,
            MemoryDeltaBytes: endMemory - _startMemory,
            NetworkBytesRead: endNetRead - _startNetworkRead,
            NetworkBytesWritten: endNetWrite - _startNetworkWrite,
            StorageBytesRead: endStoreRead - _startStorageRead,
            StorageBytesWritten: endStoreWrite - _startStorageWrite,
            GpuUsagePercent: 0 // GPU計測はプラットフォーム固有のため ProfilingScope では非対応（ProfilerEngine で取得）
        );

        // サンプルをトラッカーに記録
        _tracker.RecordSample(_operationName, _category, sample);
    }

    /// <summary>
    /// 現在のプロセスのCPU時間を取得する。
    /// プロセス情報の取得に失敗した場合は <see cref="TimeSpan.Zero"/> を返す。
    /// </summary>
    /// <returns>プロセスの合計プロセッサ時間</returns>
    private static TimeSpan GetProcessCpuTime()
    {
        try
        {
            // Process オブジェクトは使用後に確実に解放する
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime;
        }
        catch
        {
            // 一部のプラットフォームや権限環境では取得できない場合がある
            return TimeSpan.Zero;
        }
    }
}
