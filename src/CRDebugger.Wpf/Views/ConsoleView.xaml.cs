using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// ログエントリを一覧表示するコンソールビュー UserControl。
/// ログが空のときのプレースホルダー表示制御用コンバーターを登録する。
/// </summary>
public partial class ConsoleView : UserControl
{
    /// <summary>
    /// ConsoleView を初期化し、必要なコンバーターをリソースに登録する
    /// </summary>
    public ConsoleView()
    {
        // バインディング用コンバーターをリソースディクショナリに登録
        Resources["NullToCollapsedConverter"] = new NullToCollapsedConverter();
        Resources["ZeroToVisibleConverter"] = new ZeroToVisibleConverter();

        InitializeComponent();
    }
}

/// <summary>
/// null または空文字列の場合に Visibility.Collapsed を返す IValueConverter 実装。
/// ログエントリの詳細パネルなど、値がある場合のみ表示したい要素に使用する。
/// </summary>
internal sealed class NullToCollapsedConverter : IValueConverter
{
    /// <summary>
    /// null または空文字列の場合は Collapsed、それ以外は Visible を返す
    /// </summary>
    /// <param name="value">判定する値</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>null/空文字列の場合は Collapsed、それ以外は Visible</returns>
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        // null または空文字列の場合は非表示
        return value == null || (value is string s && string.IsNullOrEmpty(s))
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// 逆変換は非サポート
    /// </summary>
    /// <exception cref="NotSupportedException">常にスローされる</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 値が 0 の場合に Visible を返す IValueConverter 実装。
/// ログ件数が 0 のときに「ログがありません」などのプレースホルダーを表示するために使用する。
/// </summary>
internal sealed class ZeroToVisibleConverter : IValueConverter
{
    /// <summary>
    /// int 値が 0 の場合は Visible、非0の場合は Collapsed を返す
    /// </summary>
    /// <param name="value">判定する int 値</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>0 の場合は Visible、それ以外は Collapsed</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // int 型の場合のみ判定（0 なら表示、非0 なら非表示）
        if (value is int intVal)
            return intVal == 0 ? Visibility.Visible : Visibility.Collapsed;
        // int 以外は常に非表示
        return Visibility.Collapsed;
    }

    /// <summary>
    /// 逆変換は非サポート
    /// </summary>
    /// <exception cref="NotSupportedException">常にスローされる</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
