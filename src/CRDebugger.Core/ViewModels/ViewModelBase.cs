using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CRDebugger.Core.ViewModels;

/// <summary>
/// <see cref="INotifyPropertyChanged"/> を実装した ViewModel 基底クラス。
/// WPF・Avalonia の両フレームワークで共通利用できる。
/// <see cref="SetProperty{T}"/> によりボイラープレートコードを最小化する。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// プロパティの値が変更されたときに発火するイベント。
    /// UIバインディングエンジンがこのイベントを購読して表示を更新する。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// バッキングフィールドの値を更新し、値が変化した場合に
    /// <see cref="PropertyChanged"/> イベントを発火する。
    /// </summary>
    /// <typeparam name="T">プロパティの型</typeparam>
    /// <param name="field">更新対象のバッキングフィールドへの参照</param>
    /// <param name="value">設定する新しい値</param>
    /// <param name="propertyName">
    /// プロパティ名。<see cref="CallerMemberNameAttribute"/> により呼び出し元のメンバー名が自動的に設定される。
    /// </param>
    /// <returns>値が実際に変更された場合は <c>true</c>、同値だった場合は <c>false</c></returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        // 現在値と新値が等しい場合は変更通知を発火せず false を返す
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        // バッキングフィールドを新値で更新
        field = value;
        // プロパティ変更イベントを発火
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// <see cref="PropertyChanged"/> イベントを手動で発火する。
    /// 計算プロパティや複数プロパティへの一括通知など、
    /// <see cref="SetProperty{T}"/> では対応できないケースで使用する。
    /// </summary>
    /// <param name="propertyName">
    /// 変更を通知するプロパティ名。
    /// <see cref="CallerMemberNameAttribute"/> により呼び出し元のメンバー名が自動的に設定される。
    /// </param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        // 購読者が存在する場合のみイベントを発火（null条件演算子でスレッドセーフに呼び出し）
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
