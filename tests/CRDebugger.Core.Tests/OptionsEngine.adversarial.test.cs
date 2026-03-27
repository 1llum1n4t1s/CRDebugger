using CRDebugger.Core.Options;
using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Tests;

/// <summary>
/// OptionsEngine の嫌がらせテスト
/// </summary>
public sealed class OptionsEngineAdversarialTests
{
    // ── テスト用コンテナクラス群 ──

    public class SimpleContainer
    {
        public bool IsEnabled { get; set; } = true;
        public int Count { get; set; } = 42;
        public string Name { get; set; } = "test";
        public float Rate { get; set; } = 3.14f;
    }

    public class ReadOnlyContainer
    {
        public string ReadOnlyProp { get; } = "immutable";
        public int ComputedProp => 42;
    }

    public enum TestEnum { Alpha, Beta, Gamma }

    public class EnumContainer
    {
        public TestEnum Mode { get; set; } = TestEnum.Alpha;
    }

    public class AttributedContainer
    {
        [CRCategory("Graphics")]
        [CRDisplayName("解像度")]
        [CRSortOrder(1)]
        [CRRange(0, 100, Step = 5)]
        public int Quality { get; set; } = 50;

        [CRCategory("Graphics")]
        [CRDisplayName("垂直同期")]
        [CRSortOrder(2)]
        public bool VSync { get; set; }

        [CRCategory("Audio")]
        public float Volume { get; set; } = 0.8f;
    }

    public class ActionContainer
    {
        public int CallCount { get; private set; }

        [CRAction(Label = "リセット")]
        public void ResetAll() => CallCount++;

        [CRAction]
        [CRCategory("Debug")]
        public void ClearCache() => CallCount += 10;

        // パラメータ付きメソッドは無視されるべき
        [CRAction]
        public void MethodWithParam(int x) { }

        // 戻り値付きメソッドは無視されるべき
        [CRAction]
        public int MethodWithReturn() => 42;
    }

    public class UnsupportedTypeContainer
    {
        public DateTime Time { get; set; }
        public List<int> Items { get; set; } = new();
        public object? Anything { get; set; }
    }

    public class ManyPropertiesContainer
    {
        // 大量プロパティをリフレクションでスキャン
        public int P1 { get; set; } public int P2 { get; set; } public int P3 { get; set; }
        public int P4 { get; set; } public int P5 { get; set; } public int P6 { get; set; }
        public int P7 { get; set; } public int P8 { get; set; } public int P9 { get; set; }
        public int P10 { get; set; } public string S1 { get; set; } = "";
        public string S2 { get; set; } = ""; public string S3 { get; set; } = "";
        public bool B1 { get; set; } public bool B2 { get; set; } public bool B3 { get; set; }
        public float F1 { get; set; } public double D1 { get; set; } public decimal Dec1 { get; set; }
    }

    // ───────────────────────────────────
    // 🗡️ 境界値・極端入力 (Boundary Assault)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// コンテナなしでScanAllすると空リストが返ること
    /// </summary>
    [Fact]
    public void ScanAll_NoContainers_ReturnsEmpty()
    {
        var engine = new OptionsEngine();
        var result = engine.ScanAll();
        Assert.Empty(result);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// 読み取り専用プロパティがReadOnlyとしてスキャンされること
    /// </summary>
    [Fact]
    public void ScanAll_ReadOnlyProperties_MarkedReadOnly()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new ReadOnlyContainer());

        var result = engine.ScanAll();
        Assert.NotEmpty(result);
        var options = result.SelectMany(c => c.Options).ToList();
        Assert.All(options, o => Assert.True(o.IsReadOnly));
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// サポートされない型のプロパティが無視されること
    /// </summary>
    [Fact]
    public void ScanAll_UnsupportedTypes_Ignored()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new UnsupportedTypeContainer());

        var result = engine.ScanAll();
        var allOptions = result.SelectMany(c => c.Options).ToList();
        // DateTime, List<int>, object は対象外
        Assert.Empty(allOptions);
    }

    /// <summary>
    /// @adversarial @category boundary @severity high
    /// パラメータ付き/戻り値付きのCRActionメソッドが無視されること
    /// </summary>
    [Fact]
    public void ScanAll_InvalidActionMethods_Ignored()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new ActionContainer());

        var result = engine.ScanAll();
        var allActions = result.SelectMany(c => c.Actions).ToList();
        // ResetAllとClearCacheのみ（MethodWithParamとMethodWithReturnは除外）
        Assert.Equal(2, allActions.Count);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// Enum型プロパティのEnumNamesが正しく設定されること
    /// </summary>
    [Fact]
    public void ScanAll_EnumProperty_HasEnumNames()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new EnumContainer());

        var result = engine.ScanAll();
        var enumOpt = result.SelectMany(c => c.Options).First(o => o.Kind == OptionKind.Enum);
        Assert.NotNull(enumOpt.EnumNames);
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, enumOpt.EnumNames);
    }

    /// <summary>
    /// @adversarial @category boundary @severity medium
    /// CRRangeの値が正しくスキャンされること
    /// </summary>
    [Fact]
    public void ScanAll_RangeAttribute_ParsedCorrectly()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new AttributedContainer());

        var result = engine.ScanAll();
        var qualityOpt = result.SelectMany(c => c.Options)
            .First(o => o.DisplayName == "解像度");

        Assert.NotNull(qualityOpt.Range);
        Assert.Equal(0, qualityOpt.Range!.Min);
        Assert.Equal(100, qualityOpt.Range.Max);
        Assert.Equal(5, qualityOpt.Range.Step);
    }

    // ───────────────────────────────────
    // ⚡ 並行性・レースコンディション (Concurrency Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category concurrency @severity critical
    /// AddContainerとScanAllの同時実行がデッドロックしないこと
    /// </summary>
    [Fact]
    public async Task ConcurrentAddAndScan_NoDeadlock()
    {
        var engine = new OptionsEngine();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var adder = Task.Run(() =>
        {
            for (int i = 0; i < 100 && !cts.Token.IsCancellationRequested; i++)
            {
                engine.AddContainer(new SimpleContainer());
                Thread.Sleep(1);
            }
        }, TestContext.Current.CancellationToken);

        var scanner = Task.Run(() =>
        {
            for (int i = 0; i < 100 && !cts.Token.IsCancellationRequested; i++)
            {
                _ = engine.ScanAll();
                Thread.Sleep(1);
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(adder, scanner).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// @adversarial @category concurrency @severity high
    /// ContainersChangedイベントとScanAllの競合がクラッシュしないこと
    /// </summary>
    [Fact]
    public void ContainersChanged_DuringScan_NoCrash()
    {
        var engine = new OptionsEngine();
        var container = new SimpleContainer();
        engine.AddContainer(container);

        engine.ContainersChanged += (_, _) =>
        {
            // イベントハンドラ内からScanAll（再入）
            _ = engine.ScanAll();
        };

        // 再入してもクラッシュしない
        engine.AddContainer(new SimpleContainer());
    }

    // ───────────────────────────────────
    // 🔀 状態遷移の矛盾 (State Machine Abuse)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category state @severity high
    /// 同じコンテナを2回追加した場合のスキャン結果
    /// </summary>
    [Fact]
    public void AddContainer_SameObjectTwice_DuplicateEntries()
    {
        var engine = new OptionsEngine();
        var container = new SimpleContainer();
        engine.AddContainer(container);
        engine.AddContainer(container);

        var result = engine.ScanAll();
        var allOptions = result.SelectMany(c => c.Options).ToList();
        // 重複して登録される（仕様として確認）
        Assert.True(allOptions.Count >= 4 * 2); // SimpleContainer has 4 properties, registered twice
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// RemoveContainerで存在しないコンテナを削除してもクラッシュしないこと
    /// </summary>
    [Fact]
    public void RemoveContainer_NotFound_NoCrash()
    {
        var engine = new OptionsEngine();
        engine.RemoveContainer(new SimpleContainer()); // 登録していないものを削除
        // クラッシュしなければOK
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// Getterでプロパティ値が変更後の値を返すこと
    /// </summary>
    [Fact]
    public void Getter_ReturnsCurrentValue_AfterExternalChange()
    {
        var engine = new OptionsEngine();
        var container = new SimpleContainer { Count = 10 };
        engine.AddContainer(container);

        var result = engine.ScanAll();
        var countOpt = result.SelectMany(c => c.Options)
            .First(o => o.DisplayName.Contains("Count"));

        Assert.Equal(10, countOpt.Getter());

        // 外部からプロパティを変更
        container.Count = 999;
        Assert.Equal(999, countOpt.Getter());
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// Setterでプロパティ値が正しく変更されること
    /// </summary>
    [Fact]
    public void Setter_ChangesOriginalObject()
    {
        var engine = new OptionsEngine();
        var container = new SimpleContainer();
        engine.AddContainer(container);

        var result = engine.ScanAll();
        var enabledOpt = result.SelectMany(c => c.Options)
            .First(o => o.Kind == OptionKind.Boolean);

        enabledOpt.Setter!(false);
        Assert.False(container.IsEnabled);
    }

    /// <summary>
    /// @adversarial @category state @severity high
    /// ActionのExecuteが実際にメソッドを呼ぶこと
    /// </summary>
    [Fact]
    public void ActionExecute_InvokesMethod()
    {
        var engine = new OptionsEngine();
        var container = new ActionContainer();
        engine.AddContainer(container);

        var result = engine.ScanAll();
        var action = result.SelectMany(c => c.Actions)
            .First(a => a.Label == "リセット");

        action.Execute();
        Assert.Equal(1, container.CallCount);
    }

    // ───────────────────────────────────
    // 🎭 型パンチ (Type Punching)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category type @severity critical
    /// Setterに間違った型を渡した場合にInvalidCastExceptionがスローされること
    /// </summary>
    [Fact]
    public void Setter_WrongType_ThrowsInvalidCast()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new SimpleContainer());

        var result = engine.ScanAll();
        var intOpt = result.SelectMany(c => c.Options)
            .First(o => o.Kind == OptionKind.Integer);

        // int型プロパティに文字列を渡す
        Assert.ThrowsAny<Exception>(() => intOpt.Setter!("not a number"));
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// Setterにnullを渡した場合の動作（値型プロパティ）
    /// </summary>
    [Fact]
    public void Setter_NullForValueType_Throws()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new SimpleContainer());

        var result = engine.ScanAll();
        var intOpt = result.SelectMany(c => c.Options)
            .First(o => o.Kind == OptionKind.Integer);

        Assert.ThrowsAny<Exception>(() => intOpt.Setter!(null));
    }

    /// <summary>
    /// @adversarial @category type @severity medium
    /// Setterにnullを渡した場合の動作（string型プロパティ）
    /// </summary>
    [Fact]
    public void Setter_NullForString_Accepted()
    {
        var engine = new OptionsEngine();
        var container = new SimpleContainer();
        engine.AddContainer(container);

        var result = engine.ScanAll();
        var stringOpt = result.SelectMany(c => c.Options)
            .First(o => o.Kind == OptionKind.String);

        stringOpt.Setter!(null);
        Assert.Null(container.Name);
    }

    /// <summary>
    /// @adversarial @category type @severity high
    /// 全数値型がサポートされていること
    /// </summary>
    [Fact]
    public void ScanAll_AllNumericTypes_Detected()
    {
        var container = new AllNumericContainer();
        var engine = new OptionsEngine();
        engine.AddContainer(container);

        var result = engine.ScanAll();
        var options = result.SelectMany(c => c.Options).ToList();

        // int, long, short, byte, uint, ushort, sbyte + float, double, decimal = 10数値型
        Assert.True(options.Count >= 10);
    }

    public class AllNumericContainer
    {
        public int IntProp { get; set; }
        public long LongProp { get; set; }
        public short ShortProp { get; set; }
        public byte ByteProp { get; set; }
        public uint UIntProp { get; set; }
        public ushort UShortProp { get; set; }
        public sbyte SByteProp { get; set; }
        public float FloatProp { get; set; }
        public double DoubleProp { get; set; }
        public decimal DecimalProp { get; set; }
    }

    // ───────────────────────────────────
    // 🌪️ 環境異常 (Environmental Chaos)
    // ───────────────────────────────────

    /// <summary>
    /// @adversarial @category chaos @severity high
    /// CamelCaseの分割が正しく動作すること（特殊ケース）
    /// </summary>
    [Fact]
    public void ScanAll_CamelCaseSplitting_Works()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new SimpleContainer());

        var result = engine.ScanAll();
        var options = result.SelectMany(c => c.Options).ToList();

        var enabled = options.FirstOrDefault(o => o.DisplayName == "Is Enabled");
        Assert.NotNull(enabled);
    }

    /// <summary>
    /// @adversarial @category chaos @severity medium
    /// カテゴリでソートされること
    /// </summary>
    [Fact]
    public void ScanAll_CategoriesSorted()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new AttributedContainer());

        var result = engine.ScanAll();
        var names = result.Select(c => c.Name).ToList();

        var sorted = names.OrderBy(n => n).ToList();
        Assert.Equal(sorted, names);
    }
}
