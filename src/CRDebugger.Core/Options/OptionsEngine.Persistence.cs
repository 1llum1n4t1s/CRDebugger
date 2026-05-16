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
}
