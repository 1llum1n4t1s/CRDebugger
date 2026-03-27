namespace CRDebugger.Core.Theming;

/// <summary>
/// デバッガーウィンドウに適用するテーマの選択肢を表す列挙型。
/// <see cref="ThemeManager.SetTheme"/> に渡して使用する。
/// </summary>
public enum CRTheme
{
    /// <summary>
    /// OSのテーマ設定に追従する（既定値）。
    /// OSがダークモードの場合はダークテーマ、ライトモードの場合はライトテーマを使用する。
    /// OSのテーマ変更は <see cref="ThemeManager.NotifySystemThemeChanged"/> で通知する。
    /// </summary>
    System = 0,

    /// <summary>
    /// 明るい背景のライトテーマを固定適用する。
    /// OSの設定に関わらず常にライトカラーセットを使用する。
    /// </summary>
    Light = 1,

    /// <summary>
    /// 暗い背景のダークテーマを固定適用する。
    /// OSの設定に関わらず常にダークカラーセットを使用する。
    /// </summary>
    Dark = 2
}
