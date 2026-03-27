using System.Linq.Expressions;
using System.Reflection;
using CRDebugger.Core.Options.Attributes;

namespace CRDebugger.Core.Options;

/// <summary>
/// リフレクションでオブジェクトからオプションを自動検出するエンジン。
/// <see cref="AddContainer"/> で登録されたオブジェクトの public プロパティ・メソッドを
/// スキャンし、<see cref="OptionDescriptor"/> / <see cref="ActionDescriptor"/> に変換する。
/// </summary>
public sealed class OptionsEngine
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
            // DynamicOptionContainer は専用のスキャンロジックで処理する（リフレクション不要）
            if (container is DynamicOptionContainer dynamic)
            {
                options.AddRange(dynamic.Options);
                actions.AddRange(dynamic.Actions);
                continue;
            }

            // 通常オブジェクトはリフレクションで public プロパティとメソッドをスキャンする
            ScanProperties(container, options);
            ScanMethods(container, actions);
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

    /// <summary>
    /// コンテナオブジェクトの public インスタンスプロパティをリフレクションでスキャンし、
    /// サポートされる型のプロパティを <see cref="OptionDescriptor"/> に変換して <paramref name="results"/> へ追加する。
    /// </summary>
    /// <param name="container">スキャン対象のオブジェクト</param>
    /// <param name="results">スキャン結果を追加するリスト</param>
    private static void ScanProperties(object container, List<OptionDescriptor> results)
    {
        var type = container.GetType();

        // public かつインスタンスのプロパティのみを対象にする
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            // CROptionAttribute がなくても全 public プロパティを対象にする（SRDebugger と同じ挙動）
            // サポートされない型（クラス等）はスキップする
            if (!IsSupportedType(prop.PropertyType)) continue;

            // CRDisplayNameAttribute があれば優先して使用し、なければキャメルケースをスペース区切りに変換する
            var displayName = prop.GetCustomAttribute<CRDisplayNameAttribute>()?.Name
                ?? SplitCamelCase(prop.Name);

            // CRCategoryAttribute がなければ "General" カテゴリに分類する
            var category = prop.GetCustomAttribute<CRCategoryAttribute>()?.Name ?? "General";

            // CRSortOrderAttribute がなければソート順は 0（先頭）とする
            var sortOrder = prop.GetCustomAttribute<CRSortOrderAttribute>()?.Order ?? 0;

            // 数値範囲制約アトリビュートを取得する（スライダーの min/max/step に使用）
            var range = prop.GetCustomAttribute<CRRangeAttribute>();

            // Expression ツリーを使ってコンパイル済みのゲッターを生成する（ボクシングを最小化）
            var getter = CreateGetter(container, prop);

            // 書き込み可能なプロパティのみセッターを生成し、読み取り専用は null にする
            var setter = prop.CanWrite ? CreateSetter(container, prop) : null;

            results.Add(new OptionDescriptor
            {
                // 型の完全名とプロパティ名を組み合わせて一意の ID を生成する
                Id = $"{type.FullName}.{prop.Name}",
                DisplayName = displayName,
                Category = category,
                SortOrder = sortOrder,
                // セッターが null かどうかでも ReadOnly 判定を行う
                Kind = ResolveKind(prop.PropertyType, setter == null),
                ValueType = prop.PropertyType,
                Getter = getter,
                Setter = setter,
                Range = range,
                // enum 型の場合は選択肢の名前一覧を取得する（ドロップダウン用）
                EnumNames = prop.PropertyType.IsEnum ? Enum.GetNames(prop.PropertyType) : null
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

            // 戻り値がある（void 以外の）メソッドはスキップする
            if (method.ReturnType != typeof(void)) continue;

            // ラベルの優先順位：CRActionAttribute.Label → CRDisplayNameAttribute.Name → キャメルケース変換したメソッド名
            var label = actionAttr.Label
                ?? method.GetCustomAttribute<CRDisplayNameAttribute>()?.Name
                ?? SplitCamelCase(method.Name);

            // CRCategoryAttribute がなければ "General" カテゴリに分類する
            var category = method.GetCustomAttribute<CRCategoryAttribute>()?.Name ?? "General";

            // CRSortOrderAttribute がなければソート順は 0（先頭）とする
            var sortOrder = method.GetCustomAttribute<CRSortOrderAttribute>()?.Order ?? 0;

            // ラムダキャプチャ用にローカル変数へコピーしてクロージャの参照ずれを防ぐ
            var target = container;
            var m = method;
            results.Add(new ActionDescriptor
            {
                // 型の完全名とメソッド名を組み合わせて一意の ID を生成する
                Id = $"{type.FullName}.{method.Name}",
                Label = label,
                Category = category,
                SortOrder = sortOrder,
                // ボタン押下時にリフレクション経由でメソッドを呼び出す
                Execute = () => m.Invoke(target, null)
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

    /// <summary>
    /// 指定された型が Options エンジンでサポートされているかどうかを判定する。
    /// </summary>
    /// <param name="type">判定する型</param>
    /// <returns>サポートされている場合は <c>true</c>、それ以外は <c>false</c></returns>
    private static bool IsSupportedType(Type type)
    {
        // プリミティブ型・string・enum のみをサポート対象とする
        return type == typeof(bool) || type == typeof(int) || type == typeof(long) ||
               type == typeof(short) || type == typeof(byte) || type == typeof(uint) ||
               type == typeof(ushort) || type == typeof(sbyte) || type == typeof(float) ||
               type == typeof(double) || type == typeof(decimal) || type == typeof(string) ||
               type.IsEnum;
    }

    /// <summary>キャメルケースを検出する正規表現（コンパイル済みでパフォーマンス最適化）</summary>
    private static readonly System.Text.RegularExpressions.Regex CamelCaseRegex =
        new("([a-z])([A-Z])", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// キャメルケースの文字列をスペース区切りに変換する。
    /// 例: "MyProperty" → "My Property"
    /// </summary>
    /// <param name="input">変換するキャメルケース文字列</param>
    /// <returns>スペース区切りに変換された文字列</returns>
    private static string SplitCamelCase(string input) =>
        CamelCaseRegex.Replace(input, "$1 $2");
}
