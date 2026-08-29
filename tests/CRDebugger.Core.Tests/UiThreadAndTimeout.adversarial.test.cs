using System.Collections.Concurrent;
using CRDebugger.Core.Abstractions;
using CRDebugger.Core.BugReporter;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Options;
using CRDebugger.Core.Profiler;
using CRDebugger.Core.SystemInfo;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Core.Tests;

public sealed class UiThreadDispatchAdversarialTests
{
    private sealed class DeferredUiThread : IUiThread
    {
        private readonly ConcurrentQueue<Action> _actions = new();

        public bool IsOnUiThread => false;
        public int PendingCount => _actions.Count;
        public void Invoke(Action action) => _actions.Enqueue(action);
        public bool RunOne() => _actions.TryDequeue(out var action) && Run(action);

        private static bool Run(Action action)
        {
            action();
            return true;
        }
    }

    private sealed class TestOptions
    {
        public int Value { get; set; }
    }

    [Fact]
    public async Task Console_ClearBeforeDeferredFlush_DoesNotRestoreOldEntries()
    {
        var store = new LogStore(20, collapseDuplicates: false);
        var uiThread = new DeferredUiThread();
        using var viewModel = new ConsoleViewModel(store, uiThread);

        store.Append(CRLogLevel.Info, "test", "before-clear");
        await WaitUntilAsync(() => uiThread.PendingCount > 0);

        viewModel.ClearCommand.Execute(null);
        Assert.True(uiThread.RunOne());

        Assert.Empty(viewModel.DisplayEntries);
        Assert.Equal(0, viewModel.InfoCount);
    }

    [Fact]
    public async Task Options_BackgroundContainerChange_IsDispatchedBeforeCollectionMutation()
    {
        var engine = new OptionsEngine(false);
        var uiThread = new DeferredUiThread();
        using var viewModel = new OptionsViewModel(engine, uiThread);

        await Task.Run(() => engine.AddContainer(new TestOptions()), TestContext.Current.CancellationToken);

        Assert.Equal(1, uiThread.PendingCount);
        Assert.Empty(viewModel.FilteredCategories);
        Assert.True(uiThread.RunOne());
        Assert.Single(viewModel.FilteredCategories);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.True(condition(), "期限内に非同期処理が予約されなかった");
    }
}

public sealed class BugReportTimeoutAdversarialTests
{
    private sealed class BlockingSender : IBugReportSender
    {
        public async Task<bool> SendAsync(BugReport report, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        }
    }

    [Fact]
    public async Task CreateAndSendAsync_WithCancelableCallerToken_StillAppliesConfiguredTimeout()
    {
        var engine = new BugReportEngine(
            new LogStore(),
            new SystemInfoCollector(SystemInfoCollectionLevel.Minimal),
            new BlockingSender(),
            TimeSpan.FromMilliseconds(50));

        var operation = engine.CreateAndSendAsync(
            "timeout",
            string.Empty,
            cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }
}

public sealed class ProfilerReentrancyAdversarialTests
{
    private sealed class SlowGpuMonitor : IGpuMonitor
    {
        private int _active;
        private int _maxActive;
        private int _callCount;

        public int MaxActive => Volatile.Read(ref _maxActive);
        public int CallCount => Volatile.Read(ref _callCount);

        public double GetUsagePercent()
        {
            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            try
            {
                Thread.Sleep(100);
                Interlocked.Increment(ref _callCount);
                return 0;
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public long GetDedicatedMemoryBytes() => 0;
        public long GetSharedMemoryBytes() => 0;
        public double GetTemperatureCelsius() => -1;
        public string GetDeviceName() => "Test";

        private void UpdateMaximum(int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxActive);
                if (value <= current || Interlocked.CompareExchange(ref _maxActive, value, current) == current)
                    return;
            }
        }
    }

    [Fact]
    public async Task OnTick_WhenSamplingExceedsInterval_DoesNotOverlap()
    {
        var gpuMonitor = new SlowGpuMonitor();
        using var engine = new ProfilerEngine(TimeSpan.FromMilliseconds(1), gpuMonitor);
        engine.Start();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (gpuMonitor.CallCount < 2 && DateTime.UtcNow < deadline)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.True(gpuMonitor.CallCount >= 2, "再入防止後もサンプリングが継続すること");
        Assert.Equal(1, gpuMonitor.MaxActive);
    }
}
