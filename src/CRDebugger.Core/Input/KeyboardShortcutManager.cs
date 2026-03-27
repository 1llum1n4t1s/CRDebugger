namespace CRDebugger.Core.Input;

/// <summary>
/// キー修飾子
/// </summary>
[Flags]
public enum CRModifierKeys
{
    /// <summary>修飾キーなし</summary>
    None = 0,
    /// <summary>Ctrlキー</summary>
    Ctrl = 1,
    /// <summary>Shiftキー</summary>
    Shift = 2,
    /// <summary>Altキー</summary>
    Alt = 4,
    /// <summary>Ctrl + Shift</summary>
    CtrlShift = Ctrl | Shift,
    /// <summary>Ctrl + Alt</summary>
    CtrlAlt = Ctrl | Alt,
}

/// <summary>
/// キーの種類（プラットフォーム非依存）
/// </summary>
public enum CRKey
{
    /// <summary>キーなし</summary>
    None = 0,
    /// <summary>Escapeキー</summary>
    Escape,
    /// <summary>F1キー</summary>
    F1,
    /// <summary>F2キー</summary>
    F2,
    /// <summary>F3キー</summary>
    F3,
    /// <summary>F4キー</summary>
    F4,
    /// <summary>F5キー</summary>
    F5,
    /// <summary>F6キー</summary>
    F6,
    /// <summary>F7キー</summary>
    F7,
    /// <summary>F8キー</summary>
    F8,
    /// <summary>F9キー</summary>
    F9,
    /// <summary>F10キー</summary>
    F10,
    /// <summary>F11キー</summary>
    F11,
    /// <summary>F12キー</summary>
    F12,
    /// <summary>数字キー 0</summary>
    D0,
    /// <summary>数字キー 1</summary>
    D1,
    /// <summary>数字キー 2</summary>
    D2,
    /// <summary>数字キー 3</summary>
    D3,
    /// <summary>数字キー 4</summary>
    D4,
    /// <summary>数字キー 5</summary>
    D5,
    /// <summary>数字キー 6</summary>
    D6,
    /// <summary>数字キー 7</summary>
    D7,
    /// <summary>数字キー 8</summary>
    D8,
    /// <summary>数字キー 9</summary>
    D9,
    /// <summary>Aキー</summary>
    A,
    /// <summary>Bキー</summary>
    B,
    /// <summary>Cキー</summary>
    C,
    /// <summary>Dキー</summary>
    D,
    /// <summary>Eキー</summary>
    E,
    /// <summary>Fキー</summary>
    F,
    /// <summary>Gキー</summary>
    G,
    /// <summary>Hキー</summary>
    H,
    /// <summary>Iキー</summary>
    I,
    /// <summary>Jキー</summary>
    J,
    /// <summary>Kキー</summary>
    K,
    /// <summary>Lキー</summary>
    L,
    /// <summary>Mキー</summary>
    M,
    /// <summary>Nキー</summary>
    N,
    /// <summary>Oキー</summary>
    O,
    /// <summary>Pキー</summary>
    P,
    /// <summary>Qキー</summary>
    Q,
    /// <summary>Rキー</summary>
    R,
    /// <summary>Sキー</summary>
    S,
    /// <summary>Tキー</summary>
    T,
    /// <summary>Uキー</summary>
    U,
    /// <summary>Vキー</summary>
    V,
    /// <summary>Wキー</summary>
    W,
    /// <summary>Xキー</summary>
    X,
    /// <summary>Yキー</summary>
    Y,
    /// <summary>Zキー</summary>
    Z,
}

/// <summary>
/// キーの組み合わせ
/// </summary>
/// <param name="Key">メインキー</param>
/// <param name="Modifiers">修飾キー</param>
public sealed record KeyCombination(CRKey Key, CRModifierKeys Modifiers = CRModifierKeys.None);

/// <summary>
/// キーボードショートカット管理
/// </summary>
public sealed class KeyboardShortcutManager
{
    private readonly Dictionary<KeyCombination, Action> _shortcuts = new();
    private bool _enabled = true;

    /// <summary>ショートカットの有効/無効</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>ショートカットを登録する</summary>
    /// <param name="combination">キーの組み合わせ</param>
    /// <param name="action">ショートカット発動時に実行するアクション</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> が <c>null</c> の場合</exception>
    public void Register(KeyCombination combination, Action action)
    {
        _shortcuts[combination] = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>ショートカットを解除する</summary>
    /// <param name="combination">解除するキーの組み合わせ</param>
    public void Unregister(KeyCombination combination)
    {
        _shortcuts.Remove(combination);
    }

    /// <summary>キー押下を処理する。ショートカットが見つかった場合 <c>true</c> を返す。</summary>
    /// <param name="key">押されたキー</param>
    /// <param name="modifiers">修飾キーの状態</param>
    /// <returns>ショートカットが実行された場合は <c>true</c></returns>
    public bool HandleKeyDown(CRKey key, CRModifierKeys modifiers)
    {
        if (!_enabled) return false;

        var combination = new KeyCombination(key, modifiers);
        if (_shortcuts.TryGetValue(combination, out var action))
        {
            action();
            return true;
        }
        return false;
    }

    /// <summary>登録済みショートカット一覧を取得する</summary>
    /// <returns>キーの組み合わせとアクションのディクショナリ</returns>
    public IReadOnlyDictionary<KeyCombination, Action> GetAll() =>
        new Dictionary<KeyCombination, Action>(_shortcuts);
}
