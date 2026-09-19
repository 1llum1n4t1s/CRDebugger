using System.Windows.Forms;

namespace CRDebugger.WinForms;

/// <summary>WinForms コントロール用の拡張メソッド</summary>
internal static class ControlExtensions
{
    /// <summary>
    /// UIスレッドで安全にアクションを実行する。
    /// InvokeRequired の場合は Invoke でマーシャリングし、
    /// ハンドル未作成・破棄済み・破棄競合時は何もしない。
    /// </summary>
    public static void SafeInvoke(this Control control, Action action)
    {
        if (control.IsDisposed || !control.IsHandleCreated) return;
        if (control.InvokeRequired)
        {
            try { control.Invoke(action); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
        else
        {
            action();
        }
    }
}
