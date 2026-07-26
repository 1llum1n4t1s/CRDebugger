using CRDebugger.Core.Options;
using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Tests;

/// <summary>
/// Options 永続化（<see cref="IOptionsStore"/> / <see cref="JsonFileOptionsStore"/> /
/// <see cref="OptionsEngine"/> の配線）のテスト。
/// 「ストアを設定しても Save/Load が一度も呼ばれない」という不具合の再発防止が主目的。
/// </summary>
public sealed class OptionsPersistenceTests : IDisposable
{
    /// <summary>テストごとに使い捨てる一時ディレクトリ</summary>
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "crdebugger-tests-" + Guid.NewGuid().ToString("N"));

    public OptionsPersistenceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* 後始末の失敗はテスト結果に影響させない */ }
    }

    /// <summary>テスト用のオプションコンテナ</summary>
    private sealed class SampleOptions
    {
        [CRCategory("Gameplay")]
        public bool GodMode { get; set; }

        [CRCategory("Gameplay")]
        public int Lives { get; set; } = 3;

        [CRCategory("Gameplay")]
        public double Speed { get; set; } = 1.5;

        [CRCategory("Gameplay")]
        public string PlayerName { get; set; } = "Player";

        [CRCategory("Gameplay")]
        public DayOfWeek StartDay { get; set; } = DayOfWeek.Monday;
    }

    /// <summary>public インデクサを持つコンテナ（スキャンで例外を出さないことの確認用）</summary>
    private sealed class IndexerOptions
    {
        private readonly Dictionary<string, string> _bag = new();

        /// <summary>string を返す public インデクサ。Expression.Property では扱えない</summary>
        public string this[string key] => _bag.TryGetValue(key, out var v) ? v : string.Empty;

        /// <summary>戻り値がスカラーの int インデクサ</summary>
        public int this[int index] => index;

        public bool NormalOption { get; set; }
    }

    /// <summary>指定 ID のオプション記述子を取り出すヘルパー</summary>
    private static OptionDescriptor Find(IReadOnlyList<OptionCategory> categories, string propertyName) =>
        categories.SelectMany(c => c.Options).Single(o => o.Id.EndsWith("." + propertyName, StringComparison.Ordinal));

    // ───────────────────────────────────
    // 💾 永続化の往復
    // ───────────────────────────────────

    /// <summary>
    /// UI 経由の値変更がストアに保存され、新しいプロセス相当の再構築で復元されること。
    /// これが壊れると「設定したのに保存されない」無言のデータ消失になる。
    /// </summary>
    [Fact]
    public void OptionsStore_SetThenReload_RestoresAllSupportedKinds()
    {
        var path = Path.Combine(_tempDir, "options.json");

        // ── 1 周目: 値を変更して Flush する ──
        var store1 = new JsonFileOptionsStore(path);
        var engine1 = new OptionsEngine(requireOptInAttribute: false, optionsStore: store1);
        var target1 = new SampleOptions();
        engine1.AddContainer(target1);

        var categories1 = engine1.ScanAll();
        Find(categories1, nameof(SampleOptions.GodMode)).Setter!(true);
        Find(categories1, nameof(SampleOptions.Lives)).Setter!(99);
        Find(categories1, nameof(SampleOptions.Speed)).Setter!(2.75);
        Find(categories1, nameof(SampleOptions.PlayerName)).Setter!("ゆろち");
        Find(categories1, nameof(SampleOptions.StartDay)).Setter!(DayOfWeek.Friday);

        // ターゲットオブジェクト自体が更新されていること
        Assert.True(target1.GodMode);
        Assert.Equal(99, target1.Lives);

        engine1.FlushStore();
        Assert.True(File.Exists(path), "Flush してもファイルが生成されていない");

        // ── 2 周目: 別インスタンスで読み直すと値が復元される ──
        var store2 = new JsonFileOptionsStore(path);
        var engine2 = new OptionsEngine(requireOptInAttribute: false, optionsStore: store2);
        var target2 = new SampleOptions();
        engine2.AddContainer(target2);
        engine2.ScanAll();

        Assert.True(target2.GodMode);
        Assert.Equal(99, target2.Lives);
        Assert.Equal(2.75, target2.Speed);
        Assert.Equal("ゆろち", target2.PlayerName);
        Assert.Equal(DayOfWeek.Friday, target2.StartDay);
    }

    /// <summary>
    /// 再スキャン（コンテナ追加のたびに走る）が、実行中に変更した値を保存済みの値へ巻き戻さないこと。
    /// 復元は各オプションにつき初回 1 回だけであるべき。
    /// </summary>
    [Fact]
    public void OptionsStore_RescanAfterRuntimeChange_DoesNotRevertValue()
    {
        var path = Path.Combine(_tempDir, "options.json");

        var seedStore = new JsonFileOptionsStore(path);
        seedStore.Save("CRDebugger.Core.Tests.OptionsPersistenceTests+SampleOptions.Lives", "50");
        seedStore.Flush();

        var engine = new OptionsEngine(requireOptInAttribute: false, optionsStore: new JsonFileOptionsStore(path));
        var target = new SampleOptions();
        engine.AddContainer(target);
        engine.ScanAll();

        // 保存値が復元されている
        Assert.Equal(50, target.Lives);

        // 実行中にコード側から変更してから再スキャンする
        target.Lives = 7;
        engine.AddContainer(new SampleOptions()); // 別コンテナ追加で ScanAll が再度走る状況を再現
        engine.ScanAll();

        Assert.Equal(7, target.Lives);
    }

    /// <summary>
    /// ストア未設定時は永続化に関する副作用が一切起きず、値の設定だけが行われること。
    /// </summary>
    [Fact]
    public void OptionsEngine_WithoutStore_SetterStillWorks()
    {
        var engine = new OptionsEngine();
        var target = new SampleOptions();
        engine.AddContainer(target);

        Find(engine.ScanAll(), nameof(SampleOptions.Lives)).Setter!(42);

        Assert.Equal(42, target.Lives);
    }

    // ───────────────────────────────────
    // 🗡️ 境界値・異常系
    // ───────────────────────────────────

    /// <summary>
    /// public インデクサを持つコンテナでもスキャンが例外を出さず、
    /// 通常プロパティは正しく検出されること。
    /// インデクサを除外しないと Expression.Property が ArgumentException を投げ、
    /// Options タブが恒久的に壊れる。
    /// </summary>
    [Fact]
    public void ScanAll_ContainerWithPublicIndexer_DoesNotThrowAndSkipsIndexer()
    {
        var engine = new OptionsEngine();
        engine.AddContainer(new IndexerOptions());

        var categories = engine.ScanAll();
        var options = categories.SelectMany(c => c.Options).ToList();

        // 通常プロパティは検出される
        Assert.Contains(options, o => o.Id.EndsWith("." + nameof(IndexerOptions.NormalOption), StringComparison.Ordinal));
        // インデクサ（既定名 "Item"）は対象外
        Assert.DoesNotContain(options, o => o.Id.EndsWith(".Item", StringComparison.Ordinal));
    }

    // ───────────────────────────────────
    // ⚡ スキャン結果のキャッシュ
    // ───────────────────────────────────

    /// <summary>
    /// 同じコンテナを再スキャンしても記述子が作り直されないこと。
    /// ScanAll はコンテナ追加のたびに走るため、毎回再コンパイルすると
    /// コンテナ数 N に対して O(N^2) 回の Expression.Compile が発生する。
    /// </summary>
    [Fact]
    public void ScanAll_SameContainerRescanned_ReusesCachedDescriptors()
    {
        var engine = new OptionsEngine();
        var target = new SampleOptions();
        engine.AddContainer(target);

        var first = Find(engine.ScanAll(), nameof(SampleOptions.Lives));
        var second = Find(engine.ScanAll(), nameof(SampleOptions.Lives));

        // キャッシュが効いていれば同一インスタンスが返る
        Assert.Same(first, second);

        // 別コンテナを追加した後でも、既存コンテナ分は再生成されない
        engine.AddContainer(new SampleOptions());
        var third = engine.ScanAll()
            .SelectMany(c => c.Options)
            .First(o => ReferenceEquals(o, first));
        Assert.Same(first, third);
    }

    /// <summary>
    /// コンテナを解除するとスキャン結果も破棄され、再登録時に新しい記述子が作られること。
    /// キャッシュが残っていると、解除済みコンテナが記述子経由で GC されずリークする。
    /// </summary>
    [Fact]
    public void RemoveContainer_EvictsScanCache()
    {
        var engine = new OptionsEngine();
        var target = new SampleOptions();

        engine.AddContainer(target);
        var before = Find(engine.ScanAll(), nameof(SampleOptions.Lives));

        engine.RemoveContainer(target);
        Assert.Empty(engine.ScanAll());

        engine.AddContainer(target);
        var after = Find(engine.ScanAll(), nameof(SampleOptions.Lives));

        Assert.NotSame(before, after);

        // 再生成された記述子も正しく動作すること
        after.Setter!(11);
        Assert.Equal(11, target.Lives);
    }

    /// <summary>
    /// <see cref="DynamicOptionContainer"/> は実行時に項目が増減するため、
    /// キャッシュされず常に最新の一覧が返ること。
    /// </summary>
    [Fact]
    public void ScanAll_DynamicContainer_IsNotCached()
    {
        var engine = new OptionsEngine();
        var dynamic = new DynamicOptionContainer("Dyn");
        engine.AddContainer(dynamic);

        Assert.Empty(engine.ScanAll().SelectMany(c => c.Options));

        // 登録後に項目を追加しても、次のスキャンで反映される
        dynamic.AddBool("Flag", () => true, _ => { });

        Assert.Single(engine.ScanAll().SelectMany(c => c.Options));
    }

    /// <summary>
    /// 保存値が壊れている（型が変わった等）場合、復元をあきらめてコード側の初期値を使い、例外を出さないこと。
    /// </summary>
    [Fact]
    public void OptionsStore_CorruptedStoredValue_FallsBackToDefault()
    {
        var path = Path.Combine(_tempDir, "options.json");

        var seedStore = new JsonFileOptionsStore(path);
        // int プロパティに数値として解釈できない値を仕込む
        seedStore.Save("CRDebugger.Core.Tests.OptionsPersistenceTests+SampleOptions.Lives", "not-a-number");
        seedStore.Flush();

        var engine = new OptionsEngine(requireOptInAttribute: false, optionsStore: new JsonFileOptionsStore(path));
        var target = new SampleOptions();
        engine.AddContainer(target);

        var thrown = Record.Exception(() => engine.ScanAll());

        Assert.Null(thrown);
        Assert.Equal(3, target.Lives); // コード側の初期値のまま
    }

    /// <summary>
    /// 破損した JSON ファイルでも空ストアとして起動し、例外を出さないこと。
    /// </summary>
    [Fact]
    public void JsonFileOptionsStore_CorruptedFile_StartsEmpty()
    {
        var path = Path.Combine(_tempDir, "broken.json");
        File.WriteAllText(path, "{ this is not valid json");

        var store = new JsonFileOptionsStore(path);

        Assert.Null(store.Load("any-key"));
    }

    /// <summary>
    /// Flush は一時ファイル経由で置換するため、書き出し後に .tmp が残らないこと。
    /// 残っていると次回書き込みや配布物の混入で紛らわしい。
    /// </summary>
    [Fact]
    public void JsonFileOptionsStore_Flush_LeavesNoTempFile()
    {
        var path = Path.Combine(_tempDir, "options.json");

        var store = new JsonFileOptionsStore(path);
        store.Save("key", "value");
        store.Flush();

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"), "一時ファイルが残っている");
    }

    /// <summary>
    /// 変更が無い状態の Flush はファイルを書かないこと（無駄な I/O を避ける契約）。
    /// </summary>
    [Fact]
    public void JsonFileOptionsStore_FlushWithoutChanges_DoesNotCreateFile()
    {
        var path = Path.Combine(_tempDir, "options.json");

        var store = new JsonFileOptionsStore(path);
        store.Flush();

        Assert.False(File.Exists(path));
    }
}
