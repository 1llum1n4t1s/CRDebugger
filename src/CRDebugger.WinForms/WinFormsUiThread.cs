using CRDebugger.Core.Abstractions;

namespace CRDebugger.WinForms;

/// <summary>
/// WinForms用UIスレッドマーシャリング実装。
/// <see cref="IUiThread"/> インターフェースを実装し、
/// <see cref="Control.Invoke(Delegate)"/> を使ってUIスレッド上でアクションを安全に実行する。
/// マーシャリングの基準となるコントロールは <see cref="SetMarshalControl"/> で設定する。
/// </summary>
public sealed class WinFormsUiThread : IUiThread
{
    /// <summary>
    /// このインスタンスを生成した WinForms UI スレッドの ID。
    /// フォーム生成前でも、初期化を行った UI スレッド自身は安全に直接実行できる。
    /// </summary>
    private readonly int _uiThreadId = Environment.CurrentManagedThreadId;

    /// <summary>
    /// UIスレッド判定とマーシャリングに使用するWinFormsコントロール。
    /// フォーム初期化時に <see cref="SetMarshalControl"/> で設定される。
    /// </summary>
    private Control? _marshalControl;

    /// <summary>
    /// 現在のスレッドがUIスレッドかどうかを取得する。
    /// マーシャルコントロールが未設定・ハンドル未作成・破棄済みの場合は、
    /// このインスタンスを生成したスレッドとの一致で判定する。
    /// </summary>
    public bool IsOnUiThread
    {
        get
        {
            var control = Volatile.Read(ref _marshalControl);
            if (control == null || control.IsDisposed || !control.IsHandleCreated)
                return Environment.CurrentManagedThreadId == _uiThreadId;
            // InvokeRequired が false = 既にUIスレッド
            return !control.InvokeRequired;
        }
    }

    /// <summary>
    /// 指定したアクションをUIスレッドで実行する。
    /// 既にUIスレッド上にいる場合はそのまま同期実行し、
    /// 別スレッドの場合は <see cref="Control.Invoke(Delegate)"/> でマーシャリングする。
    /// 配送先が無い場合は、呼び出し元スレッドで誤って UI 更新を実行せず例外を返す。
    /// </summary>
    /// <param name="action">UIスレッド上で実行するアクション。</param>
    public void Invoke(Action action)
    {
        var control = Volatile.Read(ref _marshalControl);

        // フォーム生成前でも、UseWinForms を呼んだ UI スレッド自身からの初期化処理は安全に実行できる。
        if ((control == null || control.IsDisposed || !control.IsHandleCreated) &&
            Environment.CurrentManagedThreadId == _uiThreadId)
        {
            action();
            return;
        }

        // 配送先が無い別スレッドから UI 更新を実行すると、クロススレッド違反になる。
        if (control == null || control.IsDisposed || !control.IsHandleCreated)
            throw new InvalidOperationException("WinForms UI の配送先が利用できません。");

        if (!control.InvokeRequired)
        {
            action();
            return;
        }

        try
        {
            control.Invoke(action);
        }
        catch (ObjectDisposedException ex)
        {
            throw new InvalidOperationException("WinForms UI の配送中にフォームが破棄されました。", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("WinForms UI の配送先が利用できません。", ex);
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

    /// <summary>指定したコントロールが現在の配送先なら参照を解除する。</summary>
    internal void ClearMarshalControl(Control control)
    {
        Interlocked.CompareExchange(ref _marshalControl, null, control);
    }
}
