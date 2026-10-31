using CRDebugger.Core.Logging;

namespace CRDebugger.Core.Tests;

/// <summary>
/// CircularBuffer の嫌がらせテスト
/// </summary>
public sealed class CircularBufferAdversarialTests
{
    // ───────────────────────────────────
    // 🗡️ 境界値・極端入力 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// 容量0で初期化するとArgumentOutOfRangeExceptionがスローされること
    /// </summary>
    [Fact]
    public void Constructor_ZeroCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(0));
    }

    /// <summary>
    /// @adversarial @category boundary @severity critical
    /// 負の容量で初期化するとArgumentOutOfRangeExceptionがスローされること
    /// </summary>
    [Fact]
    public void Constructor_NegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(int.MinValue));
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 容量1のバッファが正常に動作すること（最小容量）
    /// </summary>
    [Fact]
    public void Capacity1_AddOverwrite_WorksCorrectly()
    {
        var buffer = new CircularBuffer<string>(1);
        Assert.Empty(buffer);

        buffer.Add("first");
        Assert.Single(buffer);
        Assert.Equal("first", buffer[0]);

        buffer.Add("second");
        Assert.Single(buffer);
        Assert.Equal("second", buffer[0]);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 負のインデックスでArgumentOutOfRangeExceptionがスローされること
    /// </summary>
    [Fact]
    public void Indexer_NegativeIndex_Throws()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(42);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[int.MinValue]);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// Count以上のインデックスでArgumentOutOfRangeExceptionがスローされること
    /// </summary>
    [Fact]
    public void Indexer_IndexBeyondCount_Throws()
    {
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[100]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[int.MaxValue]);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// null要素を格納できること
    /// </summary>
    [Fact]
    public void Add_NullElement_Accepted()
    {
        var buffer = new CircularBuffer<string?>(3);
        buffer.Add(null);
        buffer.Add("hello");
        buffer.Add(null);

        Assert.Equal(3, buffer.Count);
        Assert.Null(buffer[0]);
        Assert.Equal("hello", buffer[1]);
        Assert.Null(buffer[2]);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 大量の上書きを繰り返しても正しい順序が保たれること
    /// </summary>
    [Fact]
    public void Add_ManyOverwrites_MaintainsCorrectOrder()
    {
        var buffer = new CircularBuffer<int>(3);

        // 10000件追加（容量3に対して3333回以上の上書き）
        for (int i = 0; i < 10000; i++)
        {
            buffer.Add(i);
        }

        Assert.Equal(3, buffer.Count);
        Assert.Equal(9997, buffer[0]);
        Assert.Equal(9998, buffer[1]);
        Assert.Equal(9999, buffer[2]);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// Clear後にCount=0で再度追加できること
    /// </summary>
    [Fact]
    public void Clear_ThenAdd_WorksCorrectly()
    {
        var buffer = new CircularBuffer<int>(5);
        for (int i = 0; i < 10; i++) buffer.Add(i);

        buffer.Clear();
        Assert.Empty(buffer);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[0]);

        buffer.Add(999);
        Assert.Single(buffer);
        Assert.Equal(999, buffer[0]);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// ToListが正しいスナップショットを返すこと（上書き後も）
    /// </summary>
    [Fact]
    public void ToList_AfterWraparound_ReturnsCorrectOrder()
    {
        var buffer = new CircularBuffer<int>(3);
        for (int i = 0; i < 7; i++) buffer.Add(i);

        var list = buffer.ToList();
        Assert.Equal(new[] { 4, 5, 6 }, list);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// foreachが正しい順序で列挙されること
    /// </summary>
    [Fact]
    public void Enumeration_AfterWraparound_CorrectOrder()
    {
        var buffer = new CircularBuffer<int>(2);
        buffer.Add(10);
        buffer.Add(20);
        buffer.Add(30); // 10が上書きされる

        var items = new List<int>();
        foreach (var item in buffer) items.Add(item);

        Assert.Equal(new[] { 20, 30 }, items);
    }

    // ───────────────────────────────────
    // ⚡ 並行性・レースコンディション (Concurrency Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category concurrency @severity critical
    /// 同時にAddとToListを実行してもクラッシュしないこと
    /// （CircularBufferはスレッドセーフではないが、クラッシュ耐性を確認）
    /// </summary>
    [Fact]
    public async Task ConcurrentAddAndRead_DoesNotThrowFatalException()
    {
        var buffer = new CircularBuffer<int>(100);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var exceptions = new List<Exception>();

        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 100_000 && !cts.Token.IsCancellationRequested; i++)
            {
                try { buffer.Add(i); }
                catch (Exception ex) { lock (exceptions) exceptions.Add(ex); }
            }
        }, TestContext.Current.CancellationToken);

        var reader = Task.Run(() =>
        {
            for (int i = 0; i < 10_000 && !cts.Token.IsCancellationRequested; i++)
            {
                try { _ = buffer.ToList(); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { /* レース条件による例外は許容 */ }
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(writer, reader);

        // 致命的例外（OOM等）が発生していないこと
        Assert.DoesNotContain(exceptions, e => e is OutOfMemoryException || e is StackOverflowException);
    }

    // ───────────────────────────────────
    // 🌪️ 環境異常 (Environmental Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// IEnumerable中にAddされた場合のリスト化テスト
    /// </summary>
    [Fact]
    public void ToList_DuringModification_ReturnsConsistentSnapshot()
    {
        var buffer = new CircularBuffer<int>(100);
        for (int i = 0; i < 50; i++) buffer.Add(i);

        // ToListはスナップショットを返す（その後の変更は反映されない）
        var snapshot = buffer.ToList();
        buffer.Add(999);

        Assert.Equal(50, snapshot.Count);
        Assert.DoesNotContain(999, snapshot);
    }
}
