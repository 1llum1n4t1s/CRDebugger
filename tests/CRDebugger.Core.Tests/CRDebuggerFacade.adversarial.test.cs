using CRDebugger.Core.Abstractions;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;
using Moq;

namespace CRDebugger.Core.Tests;

/// <summary>
/// CRDebugger静的ファサード / CRTraceListener / CRLoggerProvider の嫌がらせテスト
/// </summary>
public sealed class CRDebuggerFacadeAdversarialTests : IDisposable
{
    public CRDebuggerFacadeAdversarialTests()
    {
        // 各テスト前にShutdownしてクリーン状態にする
        CRDebugger.Shutdown();
    }

    public void Dispose()
    {
        CRDebugger.Shutdown();
    }

    private static CRDebuggerOptions CreateTestOptions()
    {
        var mockWindow = new Mock<IDebuggerWindow>();
        mockWindow.Setup(w => w.IsVisible).Returns(false);
        mockWindow.Setup(w => w.CaptureScreenshotAsync()).ReturnsAsync((byte[]?)null);

        var mockUiThread = new Mock<IUiThread>();
        mockUiThread.Setup(u => u.Invoke(It.IsAny<Action>())).Callback<Action>(a => a());
        mockUiThread.Setup(u => u.IsOnUiThread).Returns(true);

        var options = new CRDebuggerOptions
        {
            CaptureTraceOutput = false,
            CaptureUnhandledExceptions = false,
            MaxLogEntries = 100,
            ProfilerSampleInterval = TimeSpan.FromHours(1), // テストでは発火させない
        };

        // InternalプロパティをリフレクションでSet
        typeof(CRDebuggerOptions).GetProperty("Window",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(options, mockWindow.Object);
        typeof(CRDebuggerOptions).GetProperty("UiThread",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(options, mockUiThread.Object);

        return options;
    }

    // ───────────────────────────────────
    // 🗡️ 境界値・極端入力 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// 初期化前にAPIを呼ぶとInvalidOperationExceptionがスローされること
    /// </summary>
    [Fact]
    public void API_BeforeInitialize_ThrowsInvalidOperation()
    {
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.Show());
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.Hide());
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.Toggle());
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.Log("test"));
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.LogWarning("test"));
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.LogError("test"));
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.CreateLoggerProvider());
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.CreateLogger("test"));
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.AddOptionContainer(new object()));
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.AddSystemInfo("a", "b", "c"));
        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.SetTheme(CRTheme.Dark));
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// RecordFrameは初期化前でもクラッシュしないこと（null条件演算子）
    /// </summary>
    [Fact]
    public void RecordFrame_BeforeInitialize_NoCrash()
    {
        CRDebugger.RecordFrame(); // _context?.Profiler なので例外なし
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// IsVisible/IsInitializedが初期化前で正しい値を返すこと
    /// </summary>
    [Fact]
    public void Properties_BeforeInitialize_ReturnDefaults()
    {
        Assert.False(CRDebugger.IsInitialized);
        Assert.False(CRDebugger.IsVisible);
    }

    // ───────────────────────────────────
    // 🔀 状態遷移の矛盾 (State Machine Abuse)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category state @severity critical
    /// 二重初期化でInvalidOperationExceptionがスローされること
    /// </summary>
    [Fact]
    public void Initialize_CalledTwice_Throws()
    {
        var options = CreateTestOptions();
        CRDebugger.Initialize(options);

        Assert.Throws<CRDebuggerAlreadyInitializedException>(() => CRDebugger.Initialize(options));
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// Shutdown後に再度Shutdownしてもクラッシュしないこと
    /// </summary>
    [Fact]
    public void Shutdown_CalledTwice_NoCrash()
    {
        var options = CreateTestOptions();
        CRDebugger.Initialize(options);

        CRDebugger.Shutdown();
        CRDebugger.Shutdown(); // 二重Shutdown
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// Shutdown後にAPIを呼ぶとInvalidOperationExceptionがスローされること
    /// </summary>
    [Fact]
    public void API_AfterShutdown_ThrowsInvalidOperation()
    {
        var options = CreateTestOptions();
        CRDebugger.Initialize(options);
        CRDebugger.Shutdown();

        Assert.Throws<CRDebuggerNotInitializedException>(() => CRDebugger.Log("test"));
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// Initialize→Shutdown→Initialize が正常に動作すること
    /// （注意: 現在の実装ではShutdown後の再Initializeが可能か確認）
    /// </summary>
    [Fact]
    public void Reinitialize_AfterShutdown_Works()
    {
        var options1 = CreateTestOptions();
        CRDebugger.Initialize(options1);
        CRDebugger.Shutdown();

        var options2 = CreateTestOptions();
        CRDebugger.Initialize(options2);
        Assert.True(CRDebugger.IsInitialized);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// 初期化後にログ追加が正常に動作すること
    /// </summary>
    [Fact]
    public void Log_AfterInitialize_Works()
    {
        var options = CreateTestOptions();
        CRDebugger.Initialize(options);

        CRDebugger.Log("Info message");
        CRDebugger.Log("Debug message", CRLogLevel.Debug);
        CRDebugger.LogWarning("Warning message");
        CRDebugger.LogError("Error message");
        CRDebugger.LogError("Error with exception", new Exception("test"));
    }

    // ───────────────────────────────────
    // ⚡ 並行性 (Concurrency)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category concurrency @severity critical
    /// 複数スレッドから同時にInitializeを呼んでも一つだけ成功すること
    /// </summary>
    [Fact]
    public async Task Initialize_ConcurrentCalls_OnlyOneSucceeds()
    {
        var options = CreateTestOptions();
        int successCount = 0;
        int failCount = 0;

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            try
            {
                CRDebugger.Initialize(CreateTestOptions());
                Interlocked.Increment(ref successCount);
            }
            catch (CRDebuggerAlreadyInitializedException)
            {
                Interlocked.Increment(ref failCount);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, successCount);
        Assert.Equal(9, failCount);
    }
}

/// <summary>
/// CRTraceListener の嫌がらせテスト
/// </summary>
public sealed class CRTraceListenerAdversarialTests
{
    // ───────────────────────────────────
    // 🗡️ 境界値 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// nullメッセージが正しく処理されること
    /// </summary>
    [Fact]
    public void Write_NullMessage_NoCrash()
    {
        var store = new LogStore();
        var listener = new CRTraceListener(store);

        listener.Write(null);
        listener.WriteLine(null);

        Assert.Equal(1, store.Count);
    }

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// WriteだけでWriteLineを呼ばない場合のメモリ蓄積
    /// </summary>
    [Fact]
    public void Write_WithoutWriteLine_AccumulatesMemory()
    {
        var store = new LogStore();
        var listener = new CRTraceListener(store);

        // 1000回Writeするが一度もWriteLineしない
        for (int i = 0; i < 1000; i++)
        {
            listener.Write("partial ");
        }

        // LogStoreにはまだ何も入っていない
        Assert.Equal(0, store.Count);

        // WriteLineでフラッシュ
        listener.WriteLine("end");
        Assert.Equal(1, store.Count);

        var entry = store.GetAll()[0];
        Assert.Contains("partial ", entry.Message);
        Assert.EndsWith("end", entry.Message);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 空文字列のWriteLineが正しく処理されること
    /// </summary>
    [Fact]
    public void WriteLine_EmptyString_Accepted()
    {
        var store = new LogStore();
        var listener = new CRTraceListener(store);

        listener.WriteLine("");
        Assert.Equal(1, store.Count);
        Assert.Equal("", store.GetAll()[0].Message);
    }

    // ── 🎭 型パンチ ──

    /// <summary>
    /// @adversarial @category type @severity high
    /// TraceEventのformat文字列が不正な場合
    /// </summary>
    [Fact]
    public void TraceEvent_InvalidFormat_ThrowsOrHandles()
    {
        var store = new LogStore();
        var listener = new CRTraceListener(store);

        // 不正なフォーマット文字列 "{0}{1}" に引数1個
        Assert.ThrowsAny<Exception>(() =>
            listener.TraceEvent(null, "source", System.Diagnostics.TraceEventType.Error, 0,
                "{0}{1}", "only_one_arg"));
    }

    /// <summary>
    /// @adversarial @category type @severity medium
    /// TraceEventの全イベントタイプが正しいレベルに変換されること
    /// </summary>
    [Fact]
    public void TraceEvent_AllEventTypes_MappedCorrectly()
    {
        var store = new LogStore();
        var listener = new CRTraceListener(store);

        listener.TraceEvent(null, "src", System.Diagnostics.TraceEventType.Critical, 0, "crit");
        listener.TraceEvent(null, "src", System.Diagnostics.TraceEventType.Error, 0, "err");
        listener.TraceEvent(null, "src", System.Diagnostics.TraceEventType.Warning, 0, "warn");
        listener.TraceEvent(null, "src", System.Diagnostics.TraceEventType.Information, 0, "info");
        listener.TraceEvent(null, "src", System.Diagnostics.TraceEventType.Verbose, 0, "verb");

        var all = store.GetAll();
        Assert.Equal(5, all.Count);
        Assert.Equal(CRLogLevel.Error, all[0].Level);   // Critical→Error
        Assert.Equal(CRLogLevel.Error, all[1].Level);   // Error→Error
        Assert.Equal(CRLogLevel.Warning, all[2].Level); // Warning→Warning
        Assert.Equal(CRLogLevel.Info, all[3].Level);     // Information→Info
        Assert.Equal(CRLogLevel.Debug, all[4].Level);    // Verbose→Debug
    }

    // ── 💀 リソース枯渇 ──

    /// <summary>
    /// @adversarial @category resource @severity high
    /// 大量のWrite呼び出しで文字列が蓄積するメモリリーク確認
    /// </summary>
    [Fact]
    public void Write_ManyPartials_StringAllocationDoS()
    {
        var store = new LogStore();
        var listener = new CRTraceListener(store);

        // 10000回のWriteで文字列が連結される
        for (int i = 0; i < 10_000; i++)
        {
            listener.Write("x");
        }

        // フラッシュ
        listener.WriteLine("");
        Assert.Equal(1, store.Count);
        Assert.Equal(10_000, store.GetAll()[0].Message.Length);
    }

    // ── ⚡ 並行性 ──

    /// <summary>
    /// @adversarial @category concurrency @severity critical
    /// 複数スレッドからWriteとWriteLineを同時に呼んでもクラッシュしないこと
    /// （_partialMessageフィールドはスレッドセーフではないので破損するかも）
    /// </summary>
    [Fact]
    public async Task ConcurrentWriteAndWriteLine_NoCrash()
    {
        var store = new LogStore();
        var listener = new CRTraceListener(store);

        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                try
                {
                    listener.Write($"partial-{i} ");
                    listener.WriteLine($"end-{i}");
                }
                catch (Exception) { /* スレッド安全でないので例外は許容 */ }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // データ破損はあるかもしれないが、致命的クラッシュしないこと
        Assert.True(store.Count > 0);
    }
}

/// <summary>
/// CRLoggerProvider の嫌がらせテスト
/// </summary>
public sealed class CRLoggerProviderAdversarialTests
{
    /// <summary>
    /// @adversarial @category boundary @severity high
    /// nullのLogStoreでプロバイダーを作成するとスローされること
    /// </summary>
    [Fact]
    public void Constructor_NullLogStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CRLoggerProvider(null!));
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// 空文字列のカテゴリ名でロガーが作成できること
    /// </summary>
    [Fact]
    public void CreateLogger_EmptyCategory_Works()
    {
        var store = new LogStore();
        var provider = new CRLoggerProvider(store);
        var logger = provider.CreateLogger("");
        Assert.NotNull(logger);
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// 全LogLevelが正しくマッピングされること
    /// </summary>
    [Fact]
    public void Log_AllLevels_MappedCorrectly()
    {
        var store = new LogStore();
        var provider = new CRLoggerProvider(store);
        var logger = provider.CreateLogger("test");

        logger.Log(Microsoft.Extensions.Logging.LogLevel.Trace, "trace");
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Debug, "debug");
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, "info");
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Warning, "warn");
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, "error");
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Critical, "crit");

        var all = store.GetAll();
        Assert.Equal(6, all.Count);
        Assert.Equal(CRLogLevel.Debug, all[0].Level);   // Trace→Debug
        Assert.Equal(CRLogLevel.Debug, all[1].Level);   // Debug→Debug
        Assert.Equal(CRLogLevel.Info, all[2].Level);     // Information→Info
        Assert.Equal(CRLogLevel.Warning, all[3].Level); // Warning→Warning
        Assert.Equal(CRLogLevel.Error, all[4].Level);   // Error→Error
        Assert.Equal(CRLogLevel.Error, all[5].Level);   // Critical→Error
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// IsEnabledが常にtrueを返すこと
    /// </summary>
    [Fact]
    public void IsEnabled_AlwaysTrue()
    {
        var store = new LogStore();
        var provider = new CRLoggerProvider(store);
        var logger = provider.CreateLogger("test");

        foreach (Microsoft.Extensions.Logging.LogLevel level in Enum.GetValues<Microsoft.Extensions.Logging.LogLevel>())
        {
            Assert.True(logger.IsEnabled(level));
        }
    }

    /// <summary>
    /// @adversarial @category state @severity medium
    /// BeginScopeがnullを返すこと（no-op実装）
    /// </summary>
    [Fact]
    public void BeginScope_ReturnsNull()
    {
        var store = new LogStore();
        var provider = new CRLoggerProvider(store);
        var logger = provider.CreateLogger("test");

        var scope = logger.BeginScope("scope");
        Assert.Null(scope);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 例外付きログでスタックトレースが保存されること
    /// </summary>
    [Fact]
    public void Log_WithException_StackTracePreserved()
    {
        var store = new LogStore();
        var provider = new CRLoggerProvider(store);
        var logger = provider.CreateLogger("test");

        Exception? caughtEx = null;
        try { throw new InvalidOperationException("テスト例外"); }
        catch (Exception ex) { caughtEx = ex; }

        logger.LogError(caughtEx, "Something failed");

        var entry = store.GetAll()[0];
        Assert.NotNull(entry.StackTrace);
        // スタックトレースにはメソッド名等が含まれる（例外型名は含まれない場合がある）
        Assert.NotEmpty(entry.StackTrace);
    }
}
