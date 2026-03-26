using CRDebugger.Core.Abstractions;

namespace CRDebugger.WinForms;

/// <summary>
/// WinForms用UIスレッドマーシャリング実装
/// Control.Invokeを使ってUIスレッドでアクションを実行する
/// </summary>
public sealed class WinFormsUiThread : IUiThread
{
    private Control? _marshalControl;

    public bool IsOnUiThread
    {
        get
        {
            if (_marshalControl == null || _marshalControl.IsDisposed)
                return true;
            return !_marshalControl.InvokeRequired;
        }
    }

    public void Invoke(Action action)
    {
        if (_marshalControl == null || _marshalControl.IsDisposed || !_marshalControl.InvokeRequired)
        {
            action();
            return;
        }

        try
        {
            _marshalControl.Invoke(action);
        }
        catch (ObjectDisposedException)
        {
            // フォームが閉じられた後のInvoke呼び出しを無視
        }
        catch (InvalidOperationException)
        {
            // ハンドル未作成時のフォールバック
            action();
        }
    }

    /// <summary>
    /// マーシャリング用コントロールを設定する（フォーム初期化時に呼ぶ）
    /// </summary>
    internal void SetMarshalControl(Control control)
    {
        _marshalControl = control;
    }
}
