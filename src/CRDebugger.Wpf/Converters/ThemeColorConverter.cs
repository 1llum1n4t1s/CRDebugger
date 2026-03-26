using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CRDebugger.Wpf.Converters;

/// <summary>
/// uint ARGB 値を SolidColorBrush に変換するコンバーター
/// </summary>
public sealed class ThemeColorConverter : IValueConverter
{
    public static ThemeColorConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is uint argb)
        {
            return new SolidColorBrush(UintToColor(argb));
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// uint ARGB から WPF Color へ変換
    /// </summary>
    public static Color UintToColor(uint argb)
    {
        var a = (byte)((argb >> 24) & 0xFF);
        var r = (byte)((argb >> 16) & 0xFF);
        var g = (byte)((argb >> 8) & 0xFF);
        var b = (byte)(argb & 0xFF);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// uint ARGB から SolidColorBrush へ変換
    /// </summary>
    public static SolidColorBrush UintToBrush(uint argb)
    {
        var brush = new SolidColorBrush(UintToColor(argb));
        brush.Freeze();
        return brush;
    }
}
