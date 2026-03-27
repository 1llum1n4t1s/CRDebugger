namespace CRDebugger.Core.Theming;

/// <summary>
/// テーマの管理と変更通知を担うクラス。
/// 現在のテーマ設定 (<see cref="CRTheme"/>) を保持し、
/// テーマ変更時に <see cref="ThemeChanged"/> イベントで購読者に通知する。
/// OSのダークモード変更を <see cref="NotifySystemThemeChanged"/> で受け取り、
/// <see cref="CRTheme.System"/> 選択時に自動的に反映する。
/// </summary>
public sealed class ThemeManager
{
    /// <summary>現在選択されているテーマ種別</summary>
    private CRTheme _currentTheme;

    /// <summary>OSがダークモードかどうかのフラグ（UIフレームワーク層から設定される）</summary>
    private bool _systemIsDark;

    /// <summary>
    /// テーマが変更された時に発火するイベント。
    /// 引数として変更後の <see cref="ThemeColors"/> が渡される。
    /// </summary>
    public event EventHandler<ThemeColors>? ThemeChanged;

    /// <summary>
    /// <see cref="ThemeManager"/> のインスタンスを生成する。
    /// </summary>
    /// <param name="initialTheme">初期テーマ（省略時は <see cref="CRTheme.System"/>）</param>
    public ThemeManager(CRTheme initialTheme = CRTheme.System)
    {
        // 指定された初期テーマを保存する
        _currentTheme = initialTheme;
    }

    /// <summary>
    /// 現在選択されているテーマ設定。
    /// 実際に適用されているカラーは <see cref="CurrentColors"/> で取得する。
    /// </summary>
    public CRTheme CurrentTheme => _currentTheme;

    /// <summary>
    /// 現在のテーマ設定に基づいて解決されたカラーセット。
    /// <see cref="CRTheme.System"/> の場合はOS設定に応じてダーク/ライトを返す。
    /// </summary>
    public ThemeColors CurrentColors => ResolveColors();

    /// <summary>
    /// テーマを変更し、<see cref="ThemeChanged"/> イベントを発火する。
    /// </summary>
    /// <param name="theme">設定するテーマ（<see cref="CRTheme"/> の値）</param>
    public void SetTheme(CRTheme theme)
    {
        // 新しいテーマを保存する
        _currentTheme = theme;
        // 購読者に変更後のカラーセットを通知する
        ThemeChanged?.Invoke(this, CurrentColors);
    }

    /// <summary>
    /// OS側のダークモード状態が変わった時に UIフレームワーク層から呼び出す。
    /// <see cref="CRTheme.System"/> が選択中の場合のみ <see cref="ThemeChanged"/> を発火する。
    /// </summary>
    /// <param name="isDark">OSがダークモードの場合 <c>true</c>、ライトモードの場合 <c>false</c></param>
    public void NotifySystemThemeChanged(bool isDark)
    {
        // OSのダークモード状態を更新する
        _systemIsDark = isDark;
        // System テーマ選択中のみ再通知する（Light/Dark 固定時は無視）
        if (_currentTheme == CRTheme.System)
        {
            ThemeChanged?.Invoke(this, CurrentColors);
        }
    }

    /// <summary>
    /// 現在の設定からカラーセットを解決する内部メソッド。
    /// Light → <see cref="ThemeColors.Light"/>
    /// Dark  → <see cref="ThemeColors.Dark"/>
    /// System → OSのダークモードフラグに応じていずれかを返す
    /// </summary>
    /// <returns>解決されたテーマカラーセット</returns>
    private ThemeColors ResolveColors() => _currentTheme switch
    {
        // Light 固定の場合はライトカラーセットを返す
        CRTheme.Light => ThemeColors.Light,
        // Dark 固定の場合はダークカラーセットを返す
        CRTheme.Dark  => ThemeColors.Dark,
        // System（その他）の場合はOSのダークモードフラグで切り替える
        _             => _systemIsDark ? ThemeColors.Dark : ThemeColors.Light
    };
}
