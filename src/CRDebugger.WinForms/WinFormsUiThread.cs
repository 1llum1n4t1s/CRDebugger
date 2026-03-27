using CRDebugger.Core.Abstractions;

namespace CRDebugger.WinForms;

/// <summary>
/// WinForms用UIスレッドマーシャリング実装。
/// <see cref="IUiThread"/> インターフェースを実装し、
/// <see cref="Control.Invoke"/> を使ってUIスレッド上でアクションを安全に実行する。
/// マーシャリングの基準となるコントロールは <see cref="SetMarshalControl"/> で設定する。
/// </summary>
public sealed class WinFormsUiThread : IUiThread
{
    /// <summary>
    /// UIスレッド判定とマーシャリングに使用するWinFormsコントロール。
    /// フォーム初期化時に <see cref="SetMarshalControl"/> で設定される。
    /// </summary>
    private Control? _marshalControl;

    /// <summary>
    /// 現在のスレッドがUIスレッドかどうかを取得する。
    /// マーシャルコントロールが未設定または破棄済みの場合は true を返す（安全側に倒す）。
    /// </summary>
    public bool IsOnUiThread
    {
        get
        {
            // コントロールが未設定または破棄済みの場合はUIスレッドとみなす
            if (_marshalControl == null || _marshalControl.IsDisposed)
                return true;
            // InvokeRequired が false = 既にUIスレッド
            return !_marshalControl.InvokeRequired;
        }
    }

    /// <summary>
    /// 指定したアクションをUIスレッドで実行する。
    /// 既にUIスレッド上にいる場合はそのまま同期実行し、
    /// 別スレッドの場合は <see cref="Control.Invoke"/> でマーシャリングする。
    /// フォームが閉じられた後の呼び出しは安全に無視される。
    /// </summary>
    /// <param name="action">UIスレッド上で実行するアクション。</param>
    public void Invoke(Action action)
    {
        // マーシャルコントロールが未設定・破棄済み・または既にUIスレッドの場合はそのまま実行
        if (_marshalControl == null || _marshalControl.IsDisposed || !_marshalControl.InvokeRequired)
        {
            action();
            return;
        }

        try
        {
            // UIスレッドにマーシャリングして実行
            _marshalControl.Invoke(action);
        }
        catch (ObjectDisposedException)
        {
            // フォームが閉じられた後のInvoke呼び出しを無視
        }
        catch (InvalidOperationException)
        {
            // ハンドル未作成時（フォーム表示前など）のフォールバック: 直接実行
            action();
        }
    }

    /// <summary>
    /// UIスレッドマーシャリングの基準となるコントロールを設定する。
    /// フォーム初期化完了後に呼び出すこと。
    /// </summary>
    /// <param name="control">マーシャリング基準に使用する <see cref="Control"/>（通常はメインフォーム）。</param>
    internal void SetMarshalControl(Control control)
    {
        _marshalControl = control;
    }
}
