using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CRDebugger.Wpf.Views;

/// <summary>
/// コンソール（ログ）ビュー
/// </summary>
public partial class ConsoleView : UserControl
{
    public ConsoleView()
    {
        // コンバーターをリソースに登録
        Resources["NullToCollapsedConverter"] = new NullToCollapsedConverter();
        Resources["ZeroToVisibleConverter"] = new ZeroToVisibleConverter();

        InitializeComponent();
    }
}

/// <summary>
/// null なら Collapsed、非null なら Visible を返すコンバーター
/// </summary>
internal sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null || (value is string s && string.IsNullOrEmpty(s))
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 0 なら Visible、非0 なら Collapsed を返すコンバーター（プレースホルダー表示用）
/// </summary>
internal sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal)
            return intVal == 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
