using System.Windows.Forms;

namespace CRDebugger.WinForms;

/// <summary>WinForms コントロール用の拡張メソッド</summary>
internal static class ControlExtensions
{
    /// <summary>
    /// UIスレッドで安全にアクションを実行する。
    /// InvokeRequired の場合は Invoke でマーシャリングし、
    /// ObjectDisposedException は握り潰す（コントロール破棄済み時の安全策）。
    /// </summary>
    public static void SafeInvoke(this Control control, Action action)
    {
        if (control.IsDisposed) return;
        if (control.InvokeRequired)
        {
            try { control.Invoke(action); }
            catch (ObjectDisposedException) { }
        }
        else
        {
            action();
        }
    }
}
