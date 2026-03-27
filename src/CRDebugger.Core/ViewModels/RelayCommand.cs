using System.Windows.Input;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// パラメータなしの汎用 <see cref="ICommand"/> 実装。
/// MVVM パターンにおいて ViewModel のメソッドをコマンドとしてバインドするために使用する。
/// </summary>
public sealed class RelayCommand : ICommand
{
    /// <summary>コマンド実行時に呼び出されるアクション</summary>
    private readonly Action _execute;

    /// <summary>
    /// コマンドの実行可否を判定するデリゲート。
    /// <c>null</c> の場合は常に実行可能と見なす。
    /// </summary>
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// <see cref="RelayCommand"/> のインスタンスを生成する
    /// </summary>
    /// <param name="execute">コマンド実行時に呼び出されるアクション</param>
    /// <param name="canExecute">
    /// コマンドの実行可否を返すデリゲート（省略可）。
    /// 省略した場合は常に実行可能として扱われる。
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> が <c>null</c> の場合にスローされる</exception>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// コマンドの実行可否が変化したことをUIに通知するイベント。
    /// <see cref="RaiseCanExecuteChanged"/> を呼び出すことで発火できる。
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// コマンドが現在実行可能かどうかを返す。
    /// <see cref="_canExecute"/> が <c>null</c> の場合は常に <c>true</c> を返す。
    /// </summary>
    /// <param name="parameter">コマンドパラメータ（このクラスでは使用しない）</param>
    /// <returns>実行可能な場合は <c>true</c></returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>
    /// コマンドを実行する。<see cref="_execute"/> を呼び出す。
    /// </summary>
    /// <param name="parameter">コマンドパラメータ（このクラスでは使用しない）</param>
    public void Execute(object? parameter) => _execute();

    /// <summary>
    /// <see cref="CanExecuteChanged"/> イベントを発火し、
    /// UIフレームワークにコマンドの実行可否の再評価を要求する
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// パラメータ付きの汎用 <see cref="ICommand"/> 実装。
/// MVVM パターンにおいてコマンドパラメータを必要とする操作をバインドするために使用する。
/// </summary>
/// <typeparam name="T">コマンドパラメータの型</typeparam>
public sealed class RelayCommand<T> : ICommand
{
    /// <summary>コマンド実行時に呼び出されるアクション（パラメータ付き）</summary>
    private readonly Action<T?> _execute;

    /// <summary>
    /// コマンドの実行可否を判定するデリゲート（パラメータ付き）。
    /// <c>null</c> の場合は常に実行可能と見なす。
    /// </summary>
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// <see cref="RelayCommand{T}"/> のインスタンスを生成する
    /// </summary>
    /// <param name="execute">コマンド実行時に呼び出されるアクション（パラメータ付き）</param>
    /// <param name="canExecute">
    /// コマンドの実行可否を返すデリゲート（省略可）。
    /// 省略した場合は常に実行可能として扱われる。
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> が <c>null</c> の場合にスローされる</exception>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// コマンドの実行可否が変化したことをUIに通知するイベント。
    /// <see cref="RaiseCanExecuteChanged"/> を呼び出すことで発火できる。
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// コマンドが現在実行可能かどうかを返す。
    /// パラメータを <typeparamref name="T"/> にキャストして <see cref="_canExecute"/> に渡す。
    /// </summary>
    /// <param name="parameter">コマンドパラメータ（<typeparamref name="T"/> 型にキャストされる）</param>
    /// <returns>実行可能な場合は <c>true</c></returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    /// <summary>
    /// コマンドを実行する。パラメータを <typeparamref name="T"/> にキャストして <see cref="_execute"/> を呼び出す。
    /// </summary>
    /// <param name="parameter">コマンドパラメータ（<typeparamref name="T"/> 型にキャストされる）</param>
    public void Execute(object? parameter) => _execute((T?)parameter);

    /// <summary>
    /// <see cref="CanExecuteChanged"/> イベントを発火し、
    /// UIフレームワークにコマンドの実行可否の再評価を要求する
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
