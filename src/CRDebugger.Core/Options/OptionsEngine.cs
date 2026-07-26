using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Options;

/// <summary>
/// リフレクションでオブジェクトからオプションを自動検出するエンジン。
/// <see cref="AddContainer"/> で登録されたオブジェクトの public プロパティ・メソッドを
/// スキャンし、<see cref="OptionDescriptor"/> / <see cref="ActionDescriptor"/> に変換する。
/// </summary>
public sealed partial class OptionsEngine
{
    /// <summary>登録済みオプションコンテナの一覧</summary>
    private readonly List<object> _containers = new();

    /// <summary>コンテナリストへのスレッドセーフなアクセスに使用する排他ロックオブジェクト</summary>
    private readonly object _lock = new();

    /// <summary>コンテナの追加・削除時に発火するイベント</summary>
    public event EventHandler? ContainersChanged;

    /// <summary>
    /// オプションコンテナを追加する。
    /// 登録後、<see cref="ContainersChanged"/> イベントを発火する。
    /// </summary>
    /// <param name="container">public プロパティがオプションとして自動検出されるオブジェクト</param>
    public void AddContainer(object container)
    {
        // 複数スレッドから同時にコンテナを追加しても安全なようにロックする
        lock (_lock)
        {
            _containers.Add(container);
        }
        // ロック外でイベントを発火してデッドロックを防ぐ
        ContainersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// オプションコンテナを削除する。
    /// 削除後、<see cref="ContainersChanged"/> イベントを発火する。
    /// </summary>
    /// <param name="container">削除するコンテナ</param>
    public void RemoveContainer(object container)
    {
        // スレッドセーフにコンテナを削除する
        lock (_lock)
        {
            _containers.Remove(container);
        }

        // スキャン結果キャッシュも破棄する。
        // 記述子は Expression.Constant でコンテナインスタンスを掴んでいるため、
        // ここで捨てないと解除済みコンテナが GC されずリークする。
        lock (_cacheLock)
        {
            _scanCache.Remove(container);
        }

        // ロック外でイベントを発火してデッドロックを防ぐ
        ContainersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 全コンテナをスキャンしてカテゴリ別にグループ化する。
    /// <see cref="DynamicOptionContainer"/> は専用パスで処理され、
    /// 通常オブジェクトはリフレクションで解析される。
    /// </summary>
    /// <returns>カテゴリ名でソートされた <see cref="OptionCategory"/> の一覧</returns>
    public IReadOnlyList<OptionCategory> ScanAll()
    {
        // スキャン結果を蓄積するリスト
        var options = new List<OptionDescriptor>();
        var actions = new List<ActionDescriptor>();

        // スキャン中にコンテナが変更されても安全なようにスナップショットを作成する
        List<object> snapshot;
        lock (_lock) { snapshot = _containers.ToList(); }

        foreach (var container in snapshot)
        {
            // DynamicOptionContainer は専用のスキャンロジックで処理する（リフレクション不要）。
            // 実行時に AddBool 等で項目が増減しうるためキャッシュせず、毎回最新の一覧を読む。
            if (container is DynamicOptionContainer dynamic)
            {
                options.AddRange(dynamic.Options);
                actions.AddRange(dynamic.Actions);
                continue;
            }

            // 通常オブジェクトはリフレクションで public プロパティとメソッドをスキャンする。
            // 結果はコンテナ単位でキャッシュする（詳細は GetOrCreateScan を参照）。
            var scan = GetOrCreateScan(container);
            options.AddRange(scan.Options);
            actions.AddRange(scan.Actions);
        }

        // 永続化ストアが設定されていれば、保存値の復元とセッターの保存ラップを施した記述子に差し替える。
        // ストア未設定時は BindPersistence が同一インスタンスを返すため、オーバーヘッドは実質ゼロ。
        if (OptionsStore != null)
        {
            for (var i = 0; i < options.Count; i++)
                options[i] = BindPersistence(options[i]);
        }

        // オプションをカテゴリ別に辞書へグループ化し、各カテゴリ内でソート順を適用する（O(n) の GroupBy で最適化）
        var optionsByCategory = options.GroupBy(o => o.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OptionDescriptor>)g.OrderBy(o => o.SortOrder).ToList());

        // アクションも同様にカテゴリ別辞書へグループ化する
        var actionsByCategory = actions.GroupBy(a => a.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ActionDescriptor>)g.OrderBy(a => a.SortOrder).ToList());

        // オプションとアクション両方のカテゴリ名を結合して重複を除去し、アルファベット順にソートする
        var categoryNames = optionsByCategory.Keys
            .Concat(actionsByCategory.Keys)
            .Distinct()
            .OrderBy(c => c);

        // カテゴリ名ごとに OptionCategory を生成して返す（存在しないカテゴリは空リストで補完）
        return categoryNames.Select(name => new OptionCategory
        {
            Name = name,
            Options = optionsByCategory.GetValueOrDefault(name, Array.Empty<OptionDescriptor>()),
            Actions = actionsByCategory.GetValueOrDefault(name, Array.Empty<ActionDescriptor>())
        }).ToList();
    }

    /// <summary>1 コンテナ分のスキャン結果</summary>
    /// <param name="Options">検出されたオプション記述子</param>
    /// <param name="Actions">検出されたアクション記述子</param>
    private sealed record ContainerScan(
        IReadOnlyList<OptionDescriptor> Options,
        IReadOnlyList<ActionDescriptor> Actions);

    /// <summary>
    /// コンテナインスタンスごとのスキャン結果キャッシュ。
    /// 参照同一性で引く（コンテナが Equals をオーバーライドしていても別インスタンスを混同しない）。
    /// </summary>
    private readonly Dictionary<object, ContainerScan> _scanCache = new(ReferenceEqualityComparer.Instance);

    /// <summary><see cref="_scanCache"/> 保護用のロックオブジェクト</summary>
    private readonly object _cacheLock = new();

    /// <summary>
    /// コンテナのスキャン結果をキャッシュ経由で取得する。
    /// <para>
    /// <see cref="ScanAll"/> はコンテナを 1 つ追加するたびに（ContainersChanged 経由で）呼ばれるため、
    /// 毎回全コンテナを再スキャンするとコンテナ数 N に対して O(N^2) 回の
    /// <c>Expression.Compile()</c> が走る。記述子はコンテナインスタンスに束縛され不変なので、
    /// インスタンス単位でキャッシュして O(N) に落とす。
    /// </para>
    /// </summary>
    /// <param name="container">スキャン対象のコンテナ</param>
    /// <returns>キャッシュ済み、または新規に生成したスキャン結果</returns>
    private ContainerScan GetOrCreateScan(object container)
    {
        lock (_cacheLock)
        {
            if (_scanCache.TryGetValue(container, out var cached)) return cached;
        }

        // Expression.Compile を含む重い処理はロック外で行い、他スレッドを待たせない
        var options = new List<OptionDescriptor>();
        var actions = new List<ActionDescriptor>();
        ScanProperties(container, options, RequireOptInAttribute);
        ScanMethods(container, actions);
        var scan = new ContainerScan(options, actions);

        lock (_cacheLock)
        {
            // 競合して他スレッドが先に登録していれば、そちらを採用して記述子の同一性を保つ
            if (_scanCache.TryGetValue(container, out var existing)) return existing;
            _scanCache[container] = scan;
            return scan;
        }
    }

    /// <summary>
    /// コンテナオブジェクトの public インスタンスプロパティをリフレクションでスキャンし、
    /// サポートされる型のプロパティを <see cref="OptionDescriptor"/> に変換して <paramref name="results"/> へ追加する。
    /// </summary>
    /// <param name="container">スキャン対象のオブジェクト</param>
    /// <param name="results">スキャン結果を追加するリスト</param>
    /// <param name="requireOptIn"><c>true</c> の場合、<see cref="CROptionAttribute"/> 付きのプロパティのみを対象にする</param>
    private static void ScanProperties(object container, List<OptionDescriptor> results, bool requireOptIn)
    {
        var type = container.GetType();

        // public かつインスタンスのプロパティのみを対象にする
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            // インデクサ（public string this[int i] 等）は引数を取るため Expression.Property で扱えず、
            // CreateGetter が ArgumentException を投げてスキャン全体を壊す。ここで確実に除外する。
            if (prop.GetIndexParameters().Length > 0) continue;

            // 書き込み専用プロパティはゲッターを生成できないため除外する（同じく CreateGetter が失敗する）
            if (!prop.CanRead) continue;

            // opt-in モードでは CROptionAttribute が付いていないプロパティをスキップする
            // （デフォルト挙動は opt-out で、CROption がなくても全 public プロパティを対象にする）
            if (requireOptIn && prop.GetCustomAttribute<CROptionAttribute>() == null) continue;

            // サポートされない型（クラス等）はスキップする
            if (!IsSupportedType(prop.PropertyType)) continue;

            // CRDisplayNameAttribute があれば優先して使用し、なければキャメルケースをスペース区切りに変換する
            var displayName = prop.GetCustomAttribute<CRDisplayNameAttribute>()?.Name
                ?? SplitCamelCase(prop.Name);

            // CRCategoryAttribute がなければ "General" カテゴリに分類する
            var category = prop.GetCustomAttribute<CRCategoryAttribute>()?.Name ?? "General";

            // CRSortOrderAttribute がなければソート順は 0（先頭）とする
            var sortOrder = prop.GetCustomAttribute<CRSortOrderAttribute>()?.Order ?? 0;

            // CRDescriptionAttribute から説明テキストを取得する
            var description = prop.GetCustomAttribute<CRDescriptionAttribute>()?.Description;

            // 数値範囲制約アトリビュートを取得する（スライダーの min/max/step に使用）
            var range = prop.GetCustomAttribute<CRRangeAttribute>();

            // CRColorAttribute が付いた string プロパティは Color ピッカーとして扱う
            var isColor = prop.GetCustomAttribute<CRColorAttribute>() != null;

            // Expression ツリーを使ってコンパイル済みのゲッターを生成する（ボクシングを最小化）
            var getter = CreateGetter(container, prop);

            // 書き込み可能なプロパティのみセッターを生成し、読み取り専用は null にする
            var setter = prop.CanWrite ? CreateSetter(container, prop) : null;

            // CRColorAttribute + string + 書き込み可能 → Color Kind、それ以外は型から判定
            var kind = isColor && prop.PropertyType == typeof(string) && setter != null
                ? OptionKind.Color
                : ResolveKind(prop.PropertyType, setter == null);

            results.Add(new OptionDescriptor
            {
                // 型の完全名とプロパティ名を組み合わせて一意の ID を生成する
                Id = $"{type.FullName}.{prop.Name}",
                DisplayName = displayName,
                Category = category,
                SortOrder = sortOrder,
                Kind = kind,
                ValueType = prop.PropertyType,
                Getter = getter,
                Setter = setter,
                Range = range,
                // enum 型の場合は選択肢の名前一覧を取得する（ドロップダウン用）
                EnumNames = prop.PropertyType.IsEnum ? Enum.GetNames(prop.PropertyType) : null,
                Description = description,
            });
        }
    }

    /// <summary>
    /// コンテナオブジェクトの public インスタンスメソッドをリフレクションでスキャンし、
    /// <see cref="CRActionAttribute"/> が付いたメソッドを <see cref="ActionDescriptor"/> に変換して
    /// <paramref name="results"/> へ追加する。
    /// </summary>
    /// <param name="container">スキャン対象のオブジェクト</param>
    /// <param name="results">スキャン結果を追加するリスト</param>
    private static void ScanMethods(object container, List<ActionDescriptor> results)
    {
        var type = container.GetType();

        // 継承メソッドを除外するため DeclaredOnly を指定する
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            // CRActionAttribute がないメソッドはボタン化しない
            var actionAttr = method.GetCustomAttribute<CRActionAttribute>();
            if (actionAttr == null) continue;

            // 引数ありのメソッドは実行できないためスキップする
            if (method.GetParameters().Length > 0) continue;

            // void または Task 戻り値のメソッドのみボタン化する
            var isAsync = method.ReturnType == typeof(Task);
            if (method.ReturnType != typeof(void) && !isAsync) continue;

            // ラベルの優先順位：CRActionAttribute.Label → CRDisplayNameAttribute.Name → キャメルケース変換したメソッド名
            var label = actionAttr.Label
                ?? method.GetCustomAttribute<CRDisplayNameAttribute>()?.Name
                ?? SplitCamelCase(method.Name);

            // CRCategoryAttribute がなければ "General" カテゴリに分類する
            var category = method.GetCustomAttribute<CRCategoryAttribute>()?.Name ?? "General";

            // CRSortOrderAttribute がなければソート順は 0（先頭）とする
            var sortOrder = method.GetCustomAttribute<CRSortOrderAttribute>()?.Order ?? 0;

            // CRDescriptionAttribute から説明テキストを取得する
            var description = method.GetCustomAttribute<CRDescriptionAttribute>()?.Description;

            // ラムダキャプチャ用にローカル変数へコピーしてクロージャの参照ずれを防ぐ
            var target = container;
            var m = method;

            // 同期/非同期に応じた ExecuteAsync デリゲートを生成する
            Func<Task> executeAsync;
            Action execute;
            if (isAsync)
            {
                // Task 戻り値のメソッドはそのまま Task にキャストして返す
                executeAsync = () => (Task)m.Invoke(target, null)!;
                execute = () => executeAsync();
            }
            else
            {
                // void メソッドは Task.CompletedTask を返すラッパーで包む
                execute = () => m.Invoke(target, null);
                executeAsync = () => { execute(); return Task.CompletedTask; };
            }

            results.Add(new ActionDescriptor
            {
                // 型の完全名とメソッド名を組み合わせて一意の ID を生成する
                Id = $"{type.FullName}.{method.Name}",
                Label = label,
                Category = category,
                SortOrder = sortOrder,
                Execute = execute,
                ExecuteAsync = executeAsync,
                Description = description,
            });
        }
    }

    /// <summary>
    /// Expression ツリーを使ってプロパティのコンパイル済みゲッターデリゲートを生成する。
    /// リフレクションの <c>GetValue</c> より高速に動作する。
    /// </summary>
    /// <param name="target">プロパティを保持するオブジェクトインスタンス</param>
    /// <param name="prop">ゲッターを生成する対象のプロパティ情報</param>
    /// <returns>プロパティ値を <c>object?</c> として返すデリゲート</returns>
    private static Func<object?> CreateGetter(object target, PropertyInfo prop)
    {
        // ターゲットオブジェクトを定数式として埋め込む
        var instance = Expression.Constant(target);

        // プロパティアクセス式を構築する（例: target.MyProperty）
        var access = Expression.Property(instance, prop);

        // 戻り値を object にボックス化する変換式を追加する
        var convert = Expression.Convert(access, typeof(object));

        // 引数なしラムダとしてコンパイルして高速なデリゲートを得る
        var lambda = Expression.Lambda<Func<object?>>(convert);
        return lambda.Compile();
    }

    /// <summary>
    /// Expression ツリーを使ってプロパティのコンパイル済みセッターデリゲートを生成する。
    /// リフレクションの <c>SetValue</c> より高速に動作する。
    /// </summary>
    /// <param name="target">プロパティを保持するオブジェクトインスタンス</param>
    /// <param name="prop">セッターを生成する対象のプロパティ情報</param>
    /// <returns><c>object?</c> 型の値を受け取りプロパティへ設定するデリゲート</returns>
    private static Action<object?> CreateSetter(object target, PropertyInfo prop)
    {
        // ターゲットオブジェクトを定数式として埋め込む
        var instance = Expression.Constant(target);

        // object? 型のパラメーター式を定義する（セッターに渡される値）
        var param = Expression.Parameter(typeof(object), "value");

        // object? からプロパティの実際の型へのキャスト変換式を追加する
        var convert = Expression.Convert(param, prop.PropertyType);

        // プロパティへの代入式を構築する（例: target.MyProperty = (T)value）
        var assign = Expression.Assign(Expression.Property(instance, prop), convert);

        // 単一パラメーターのラムダとしてコンパイルして高速なデリゲートを得る
        var lambda = Expression.Lambda<Action<object?>>(assign, param);
        return lambda.Compile();
    }

    /// <summary>
    /// プロパティの型と読み取り専用フラグから UI コントロールの種類を決定する。
    /// </summary>
    /// <param name="type">プロパティの型</param>
    /// <param name="isReadOnly">プロパティが読み取り専用かどうか</param>
    /// <returns>対応する <see cref="OptionKind"/> の値</returns>
    private static OptionKind ResolveKind(Type type, bool isReadOnly)
    {
        // 読み取り専用プロパティは表示のみ（編集不可）
        if (isReadOnly) return OptionKind.ReadOnly;

        // bool → チェックボックス
        if (type == typeof(bool)) return OptionKind.Boolean;

        // 整数系（符号あり・なし問わず）→ 整数入力
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
            type == typeof(byte) || type == typeof(uint) || type == typeof(ushort) ||
            type == typeof(sbyte)) return OptionKind.Integer;

        // 浮動小数点数・固定小数点数 → 小数入力
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return OptionKind.Float;

        // string → テキスト入力
        if (type == typeof(string)) return OptionKind.String;

        // enum → ドロップダウン選択
        if (type.IsEnum) return OptionKind.Enum;

        // 上記以外はサポート外のため読み取り専用表示にフォールバックする
        return OptionKind.ReadOnly;
    }

    /// <summary>サポート対象のプリミティブ型セット（O(1)ルックアップ用）</summary>
    private static readonly HashSet<Type> s_supportedTypes =
    [
        typeof(bool), typeof(int), typeof(long), typeof(short), typeof(byte),
        typeof(uint), typeof(ushort), typeof(sbyte), typeof(float),
        typeof(double), typeof(decimal), typeof(string)
    ];

    /// <summary>
    /// 指定された型が Options エンジンでサポートされているかどうかを判定する。
    /// </summary>
    /// <param name="type">判定する型</param>
    /// <returns>サポートされている場合は <c>true</c>、それ以外は <c>false</c></returns>
    private static bool IsSupportedType(Type type) =>
        s_supportedTypes.Contains(type) || type.IsEnum;

    /// <summary>キャメルケースを検出するソースジェネレーター生成正規表現（起動時JITコンパイル不要）</summary>
    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex CamelCaseRegex();

    /// <summary>
    /// キャメルケースの文字列をスペース区切りに変換する。
    /// 例: "MyProperty" → "My Property"
    /// </summary>
    /// <param name="input">変換するキャメルケース文字列</param>
    /// <returns>スペース区切りに変換された文字列</returns>
    private static string SplitCamelCase(string input) =>
        CamelCaseRegex().Replace(input, "$1 $2");
}
