using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// バグレポート送信フォームを表示する UserControl。
/// コンバーターをリソースとして登録し、XAML バインディングで使用できるようにする。
/// </summary>
public partial class BugReporterView : UserControl
{
    /// <summary>
    /// BugReporterView を初期化し、必要なコンバーターをリソースに登録する
    /// </summary>
    public BugReporterView()
    {
        // バインディング用コンバーターをリソースディクショナリに登録
        Resources["InverseBoolConverter"] = new InverseBoolConverter();
        Resources["NullOrEmptyToCollapsedConverter"] = new NullOrEmptyToCollapsedConverter();

        InitializeComponent();
    }
}

/// <summary>
/// bool 値を反転する IValueConverter 実装。
/// 送信中は入力フォームを無効化するなど、IsEnabled の逆転に使用する。
/// </summary>
internal sealed class InverseBoolConverter : IValueConverter
{
    /// <summary>
    /// bool 値を反転して返す
    /// </summary>
    /// <param name="value">反転する bool 値</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>bool の場合は反転値、それ以外は true</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // bool 型の場合は反転、それ以外はデフォルトで true を返す
        if (value is bool b)
            return !b;
        return true;
    }

    /// <summary>
    /// bool 値を反転して返す（双方向バインディング用）
    /// </summary>
    /// <param name="value">反転する bool 値</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>bool の場合は反転値、それ以外は false</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // bool 型の場合は反転、それ以外はデフォルトで false を返す
        if (value is bool b)
            return !b;
        return false;
    }
}

/// <summary>
/// null または空文字列の場合に Visibility.Collapsed を返す IValueConverter 実装。
/// エラーメッセージや結果テキストの表示制御に使用する。
/// </summary>
internal sealed class NullOrEmptyToCollapsedConverter : IValueConverter
{
    /// <summary>
    /// 文字列が null または空の場合は Collapsed、それ以外は Visible を返す
    /// </summary>
    /// <param name="value">判定する値（string を期待）</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>null/空文字列の場合は Collapsed、それ以外は Visible</returns>
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        // 非空文字列の場合のみ表示、null・空文字列は非表示
        return value is string s && !string.IsNullOrEmpty(s)
            ? Visibility.Visible
            : Visibility.Collapsed;
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
