namespace CRDebugger.Core.Input;

/// <summary>
/// キー修飾子
/// </summary>
[Flags]
public enum CRModifierKeys
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    CtrlShift = Ctrl | Shift,
    CtrlAlt = Ctrl | Alt,
}

/// <summary>
/// キーの種類（プラットフォーム非依存）
/// </summary>
public enum CRKey
{
    None = 0,
    Escape, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
}

/// <summary>
/// キーの組み合わせ
/// </summary>
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

    /// <summary>ショートカットを登録</summary>
    public void Register(KeyCombination combination, Action action)
    {
        _shortcuts[combination] = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>ショートカットを解除</summary>
    public void Unregister(KeyCombination combination)
    {
        _shortcuts.Remove(combination);
    }

    /// <summary>キー押下を処理。ショートカットが見つかった場合trueを返す。</summary>
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

    /// <summary>登録済みショートカット一覧</summary>
    public IReadOnlyDictionary<KeyCombination, Action> GetAll() =>
        new Dictionary<KeyCombination, Action>(_shortcuts);
}
