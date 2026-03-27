using Avalonia.Threading;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Avalonia;

/// <summary>
/// Avalonia の <see cref="Dispatcher.UIThread"/> を使った <see cref="IUiThread"/> 実装。
/// UIスレッドへのアクション投入とスレッド判定を担当する。
/// </summary>
public sealed class AvaloniaUiThread : IUiThread
{
    /// <summary>
    /// 現在のスレッドが UI スレッドかどうかを取得する。
    /// </summary>
    public bool IsOnUiThread => Dispatcher.UIThread.CheckAccess();

    /// <summary>
    /// 指定したアクションを UI スレッドで実行する。
    /// 既に UI スレッド上であれば同期的に直接実行し、
    /// 別スレッドからの呼び出しの場合は <see cref="Dispatcher.UIThread"/> にポストする。
    /// </summary>
    /// <param name="action">UIスレッドで実行するアクション</param>
    public void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            // 既に UI スレッド上にいるので直接実行する
            action();
        }
        else
        {
            // 別スレッドからの呼び出しなので UI スレッドにポストする
            Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
        }
    }
}
