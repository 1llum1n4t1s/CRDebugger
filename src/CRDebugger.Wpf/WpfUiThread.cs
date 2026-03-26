using System.Windows;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Wpf;

/// <summary>
/// WPF Dispatcher を使った IUiThread 実装
/// </summary>
public sealed class WpfUiThread : IUiThread
{
    public bool IsOnUiThread =>
        Application.Current?.Dispatcher.CheckAccess() ?? false;

    public void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }
}
