using System.Globalization;

namespace CRDebugger.Core.Options;

/// <summary>
/// <see cref="OptionsEngine"/> の永続化 + opt-in 設定の拡張部分。
/// CRDebuggerContext から構成オプションを受け取り、ScanProperties の挙動と
/// IOptionsStore による Save/Load を制御する。
/// </summary>
public sealed partial class OptionsEngine
{
    /// <summary>opt-in モード（CROption 必須）か（コンストラクタ引数で設定）</summary>
    internal bool RequireOptInAttribute { get; }

    /// <summary>Options 永続化ストア（null = 永続化しない）</summary>
    internal IOptionsStore? OptionsStore { get; }

    /// <summary>
    /// オプション付きで OptionsEngine を構築する。
    /// </summary>
    /// <param name="requireOptInAttribute">CROptionAttribute 必須モード（デフォルト false）</param>
    /// <param name="optionsStore">永続化ストア（デフォルト null）</param>
    public OptionsEngine(bool requireOptInAttribute = false, IOptionsStore? optionsStore = null)
    {
        RequireOptInAttribute = requireOptInAttribute;
        OptionsStore = optionsStore;
    }

    /// <summary>
    /// 保留中の Options 変更を永続化ストアにフラッシュする（Shutdown 時に呼ばれる）。
    /// </summary>
    public void FlushStore()
    {
        OptionsStore?.Flush();
    }

    /// <summary>
    /// 既に復元処理を適用した記述子 ID の集合。
    /// <see cref="ScanAll"/> はコンテナ追加のたびに呼ばれるため、
    /// 「保存値の復元は各オプションにつき 1 回だけ」を保証してユーザーの実行時変更を上書きしないようにする。
    /// </summary>
    private readonly HashSet<string> _restoredIds = new(StringComparer.Ordinal);

    /// <summary>
    /// オプション記述子を永続化ストアに接続する。
    /// <list type="number">
    ///   <item>初回のみ、保存済みの値をターゲットプロパティへ復元する</item>
    ///   <item>セッターを「本来の設定 → ストアへ保存」でラップする</item>
    /// </list>
    /// ストア未設定・読み取り専用オプションの場合は元の記述子をそのまま返す。
    /// </summary>
    /// <param name="descriptor">接続対象の記述子</param>
    /// <returns>永続化に接続された記述子（未接続の場合は引数と同一インスタンス）</returns>
    private OptionDescriptor BindPersistence(OptionDescriptor descriptor)
    {
        var store = OptionsStore;
        var inner = descriptor.Setter;

        // ストア未設定、または読み取り専用（セッターなし）は永続化対象外
        if (store == null || inner == null) return descriptor;

        var id = descriptor.Id;
        var valueType = descriptor.ValueType;

        // 保存値の復元は 1 度だけ行う（再スキャンのたびに巻き戻すと実行時の変更が消えるため）
        if (_restoredIds.Add(id))
        {
            try
            {
                var stored = store.Load(id);
                if (stored != null)
                    inner(StringToValue(stored, valueType));
            }
            catch (Exception)
            {
                // 保存値が壊れている / 型が変わった場合は復元をあきらめ、コード側の初期値を使う。
                // ホストアプリを巻き込まないためここで握りつぶす。
            }
        }

        return descriptor.WithSetter(value =>
        {
            // 先に本来の設定を行う。型変換失敗などで例外が出た場合は保存しない
            inner(value);

            try
            {
                store.Save(id, ValueToString(value));
            }
            catch (Exception)
            {
                // 永続化の失敗はホストアプリを巻き込まない（次回の Save / Flush で再試行される）
            }
        });
    }

    /// <summary>
    /// オプション値を永続化用の文字列に変換する。
    /// カルチャ差で小数点記号が変わらないよう、常にインバリアントカルチャを使う。
    /// </summary>
    /// <param name="value">保存する値</param>
    /// <returns>値の文字列表現（null は空文字列）</returns>
    private static string ValueToString(object? value) => value switch
    {
        null => string.Empty,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// 永続化された文字列をオプションの実型へ復元する。
    /// </summary>
    /// <param name="stored">保存されていた文字列</param>
    /// <param name="targetType">復元先の型</param>
    /// <returns>復元された値</returns>
    private static object? StringToValue(string stored, Type targetType)
    {
        // enum は名前で保存されているため Enum.Parse で戻す
        if (targetType.IsEnum) return Enum.Parse(targetType, stored);

        // string はそのまま（空文字列も有効な値として扱う）
        if (targetType == typeof(string)) return stored;

        return Convert.ChangeType(stored, targetType, CultureInfo.InvariantCulture);
    }
}
