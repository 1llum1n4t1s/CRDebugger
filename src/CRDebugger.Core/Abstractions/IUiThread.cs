namespace CRDebugger.Core.Abstractions;

/// <summary>
/// UIスレッドへのマーシャリング抽象。
/// バックグラウンドスレッドからUI操作を安全に行うために使用する。
/// 各UIフレームワーク（Avalonia・WPF・WinForms）が固有のディスパッチャーを使って実装する。
/// </summary>
public interface IUiThread
{
    /// <summary>
    /// 現在のスレッドがUIスレッドかどうかを示す値。
    /// <c>true</c> の場合は直接UI操作が可能。<c>false</c> の場合は <see cref="Invoke"/> 経由で操作すること。
    /// </summary>
    bool IsOnUiThread { get; }

    /// <summary>
    /// UIスレッド上でアクションを実行する。
    /// すでにUIスレッド上にいる場合は直接実行し、そうでない場合はディスパッチャーにキューイングする。
    /// </summary>
    /// <param name="action">UIスレッド上で実行するアクション</param>
    void Invoke(Action action);
}
