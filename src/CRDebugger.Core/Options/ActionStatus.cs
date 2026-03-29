namespace CRDebugger.Core.Options;

/// <summary>
/// アクションボタンの実行状態を表す列挙型。
/// UI でスピナー・成功/失敗アイコンの切り替えに使用する。
/// </summary>
public enum ActionStatus
{
    /// <summary>通常状態（ボタン操作可能）</summary>
    Idle,
    /// <summary>実行中（スピナー表示、ボタン無効化）</summary>
    Running,
    /// <summary>成功（緑チェック表示、2秒後に Idle へ戻る）</summary>
    Success,
    /// <summary>失敗（赤×表示、2秒後に Idle へ戻る）</summary>
    Failed
}
