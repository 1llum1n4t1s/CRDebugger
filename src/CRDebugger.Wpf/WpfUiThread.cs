using System.Windows;
using CRDebugger.Core.Abstractions;

namespace CRDebugger.Wpf;

/// <summary>
/// WPF の Application.Current.Dispatcher を使って UI スレッドへの同期を提供する
/// IUiThread インターフェースの実装クラス。
/// </summary>
public sealed class WpfUiThread : IUiThread
{
    /// <summary>
    /// 現在のスレッドが WPF の UI スレッドかどうかを取得する。
    /// Application.Current が null の場合は false を返す。
    /// </summary>
    public bool IsOnUiThread =>
        Application.Current?.Dispatcher.CheckAccess() ?? false;

    /// <summary>
    /// 指定したアクションを UI スレッドで同期実行する。
    /// すでに UI スレッド上の場合は直接呼び出し、
    /// 別スレッドからの場合は Dispatcher.Invoke でマーシャリングする。
    /// Application.Current が null の場合はそのまま直接実行する。
    /// </summary>
    /// <param name="action">UI スレッドで実行するアクション</param>
    public void Invoke(Action action)
    {
        // Application.Current が null の場合（テスト環境など）はそのまま実行
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }

        // すでに UI スレッド上にいる場合はそのまま直接実行（デッドロック回避）
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            // 別スレッドからの場合は Dispatcher.Invoke で UI スレッドに同期マーシャリング
            dispatcher.Invoke(action);
        }
    }
}
