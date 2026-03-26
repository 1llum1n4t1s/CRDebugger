namespace CRDebugger.Core.Abstractions;

/// <summary>
/// UIスレッドへのマーシャリング抽象
/// </summary>
public interface IUiThread
{
    bool IsOnUiThread { get; }
    void Invoke(Action action);
}
