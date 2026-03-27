namespace CRDebugger.Core.Theming;

/// <summary>
/// テーマカラーセットを保持する不変値型（ARGB形式 uint）。
/// ダークテーマ用のプリセット <see cref="Dark"/> と
/// ライトテーマ用のプリセット <see cref="Light"/> を静的プロパティとして提供する。
/// UIフレームワーク層では各プラットフォームの Color 型に変換して使用すること。
/// </summary>
public readonly struct ThemeColors
{
    /// <summary>
    /// ウィンドウ・画面全体の背景色。
    /// 最も奥のレイヤーに使用する。
    /// </summary>
    public uint Background { get; init; }

    /// <summary>
    /// カードやパネル等のサーフェス色。
    /// 背景より一段手前のレイヤーに使用する。
    /// </summary>
    public uint Surface { get; init; }

    /// <summary>
    /// 代替サーフェス色。
    /// ホバー状態や入力欄など Surface と区別したい箇所に使用する。
    /// </summary>
    public uint SurfaceAlt { get; init; }

    /// <summary>
    /// プライマリカラー（アクセントカラー）。
    /// 選択中タブのハイライト・ボタン・リンク等に使用する。
    /// </summary>
    public uint Primary { get; init; }

    /// <summary>
    /// <see cref="Background"/> 上に配置するテキストの色。
    /// 主要な本文テキストに使用する。
    /// </summary>
    public uint OnBackground { get; init; }

    /// <summary>
    /// <see cref="Surface"/> 上に配置するテキストの色。
    /// カード内の通常テキストに使用する。
    /// </summary>
    public uint OnSurface { get; init; }

    /// <summary>
    /// <see cref="Surface"/> 上に配置するミュートされたテキストの色。
    /// ラベルや補足説明など目立たせたくないテキストに使用する。
    /// </summary>
    public uint OnSurfaceMuted { get; init; }

    /// <summary>
    /// ボーダー（区切り線・枠線）の色。
    /// 半透明の白または黒で設定することが多い。
    /// </summary>
    public uint Border { get; init; }

    /// <summary>
    /// Debug レベルログのテキスト色。
    /// 詳細なデバッグ情報の表示に使用する。
    /// </summary>
    public uint LogDebug { get; init; }

    /// <summary>
    /// Info レベルログのテキスト色。
    /// 通常の情報メッセージの表示に使用する。
    /// </summary>
    public uint LogInfo { get; init; }

    /// <summary>
    /// Warning レベルログのテキスト色。
    /// 注意が必要な警告メッセージの表示に使用する。
    /// </summary>
    public uint LogWarning { get; init; }

    /// <summary>
    /// Error レベルログのテキスト色。
    /// エラーメッセージの表示に使用する。
    /// </summary>
    public uint LogError { get; init; }

    /// <summary>
    /// 成功状態を示す色。
    /// 操作完了・正常応答など肯定的なフィードバックに使用する。
    /// </summary>
    public uint Success { get; init; }

    /// <summary>
    /// サイドバー（タブナビゲーション領域）の背景色。
    /// メインの <see cref="Background"/> より暗くすることでサイドバーを区別する。
    /// </summary>
    public uint SidebarBackground { get; init; }

    /// <summary>
    /// サイドバー内のテキスト・アイコンの色。
    /// 非選択タブのラベルに使用する。
    /// </summary>
    public uint SidebarText { get; init; }

    /// <summary>
    /// 選択中タブのアクセント色。
    /// <see cref="Primary"/> と同値にすることが多い。
    /// </summary>
    public uint SelectedTab { get; init; }

    /// <summary>
    /// モダンダークテーマのプリセット（アクリル効果対応）。
    /// 深みのある紺色ベースの配色で、ログ色は視認性を重視した落ち着いたトーン。
    /// </summary>
    public static ThemeColors Dark { get; } = new()
    {
        // ── 背景・サーフェス ─────────────────────────────────────────────
        Background    = 0xFF1A1A2E, // 深い紺色の背景
        Surface       = 0xFF232338, // カード・パネル用の少し明るい紺
        SurfaceAlt    = 0xFF2D2D45, // ホバーや入力欄用のさらに明るい紺
        // ── アクセント ───────────────────────────────────────────────────
        Primary       = 0xFF7C8FFF, // 柔らかい青紫のアクセント
        // ── テキスト ─────────────────────────────────────────────────────
        OnBackground  = 0xFFF0F0F5, // ほぼ白のメインテキスト
        OnSurface     = 0xFFC0C0D5, // やや暗めのサーフェステキスト
        OnSurfaceMuted= 0xFF9090A5, // ミュートされた補足テキスト
        // ── ボーダー ─────────────────────────────────────────────────────
        Border        = 0x10FFFFFF, // 10%不透明度の白（微細な区切り線）
        // ── ログレベル色 ─────────────────────────────────────────────────
        LogDebug      = 0xFF6CAEDD, // 水色系のデバッグログ
        LogInfo       = 0xFFB0B0C0, // グレー系の情報ログ
        LogWarning    = 0xFFE8C44A, // アンバー系の警告ログ
        LogError      = 0xFFE05252, // 赤系のエラーログ
        // ── その他 ───────────────────────────────────────────────────────
        Success       = 0xFF4CAF50, // グリーン系の成功色
        SidebarBackground = 0xFF12121C, // 背景よりさらに暗いサイドバー
        SidebarText   = 0xFF9090A5, // ミュートされたサイドバーテキスト
        SelectedTab   = 0xFF7C8FFF, // Primary と同値の選択タブ色
    };

    /// <summary>
    /// モダンライトテーマのプリセット。
    /// 白とライトグレーベースの配色で、ログ色はコントラストを確保した濃いトーン。
    /// </summary>
    public static ThemeColors Light { get; } = new()
    {
        // ── 背景・サーフェス ─────────────────────────────────────────────
        Background    = 0xFFF5F5FA, // オフホワイトの背景
        Surface       = 0xFFFFFFFF, // 純白のカード・パネル
        SurfaceAlt    = 0xFFF0F0F5, // ホバーや入力欄用のライトグレー
        // ── アクセント ───────────────────────────────────────────────────
        Primary       = 0xFF4A6CF7, // 鮮やかな青のアクセント
        // ── テキスト ─────────────────────────────────────────────────────
        OnBackground  = 0xFF1A1A2E, // ほぼ黒のメインテキスト
        OnSurface     = 0xFF333344, // ダークグレーのサーフェステキスト
        OnSurfaceMuted= 0xFF707085, // ミュートされた補足テキスト
        // ── ボーダー ─────────────────────────────────────────────────────
        Border        = 0x10000000, // 10%不透明度の黒（微細な区切り線）
        // ── ログレベル色 ─────────────────────────────────────────────────
        LogDebug      = 0xFF1565C0, // 濃い青のデバッグログ
        LogInfo       = 0xFF555566, // ダークグレーの情報ログ
        LogWarning    = 0xFFE65100, // ディープオレンジの警告ログ
        LogError      = 0xFFB71C1C, // ダークレッドのエラーログ
        // ── その他 ───────────────────────────────────────────────────────
        Success       = 0xFF2E7D32, // ダークグリーンの成功色
        SidebarBackground = 0xFFE8E8F0, // 背景より少し暗いサイドバー
        SidebarText   = 0xFF505068, // サイドバー用の中間グレー
        SelectedTab   = 0xFF4A6CF7, // Primary と同値の選択タブ色
    };
}
