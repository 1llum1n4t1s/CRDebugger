using Avalonia.Threading;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Avalonia;

/// <summary>
/// Avalonia Dispatcher を使った IUiThread 実装
/// </summary>
public sealed class AvaloniaUiThread : IUiThread
{
    public bool IsOnUiThread => Dispatcher.UIThread.CheckAccess();

    public void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
        }
    }
}
