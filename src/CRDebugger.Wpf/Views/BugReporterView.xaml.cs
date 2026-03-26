using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// バグレポートビュー
/// </summary>
public partial class BugReporterView : UserControl
{
    public BugReporterView()
    {
        // コンバーターをリソースに登録
        Resources["InverseBoolConverter"] = new InverseBoolConverter();
        Resources["NullOrEmptyToCollapsedConverter"] = new NullOrEmptyToCollapsedConverter();

        InitializeComponent();
    }
}

/// <summary>
/// bool を反転するコンバーター
/// </summary>
internal sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}

/// <summary>
/// null / 空文字 なら Collapsed を返すコンバーター
/// </summary>
internal sealed class NullOrEmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string s && !string.IsNullOrEmpty(s)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
