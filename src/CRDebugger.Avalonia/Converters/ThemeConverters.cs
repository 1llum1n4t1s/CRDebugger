using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CRDebugger.Core;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Options;

namespace CRDebugger.Avalonia.Converters;

/// <summary>
/// ARGB uint → SolidColorBrush コンバーター
/// </summary>
public sealed class UIntToColorBrushConverter : IValueConverter
{
    public static UIntToColorBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is uint argb)
        {
            var color = Color.FromUInt32(argb);
            return new SolidColorBrush(color);
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// CRTab == parameter → bool コンバーター（タブ選択用）
/// </summary>
public sealed class TabEqualsConverter : IValueConverter
{
    public static TabEqualsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CRTab tab && parameter is string tabName)
        {
            return Enum.TryParse<CRTab>(tabName, out var target) && tab == target;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string tabName && Enum.TryParse<CRTab>(tabName, out var target))
        {
            return target;
        }
        return CRTab.Console;
    }
}

/// <summary>
/// CRLogLevel → 表示色 Brush コンバーター
/// </summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public static LogLevelToBrushConverter Instance { get; } = new();

    private static readonly SolidColorBrush DebugBrush = new(Color.FromUInt32(0xFF6CAEDD));
    private static readonly SolidColorBrush InfoBrush = new(Color.FromUInt32(0xFFB0B0B0));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromUInt32(0xFFE8C44A));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromUInt32(0xFFE05252));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            CRLogLevel.Debug => DebugBrush,
            CRLogLevel.Info => InfoBrush,
            CRLogLevel.Warning => WarningBrush,
            CRLogLevel.Error => ErrorBrush,
            _ => InfoBrush,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// CRLogLevel → レベル表示文字列コンバーター
/// </summary>
public sealed class LogLevelToStringConverter : IValueConverter
{
    public static LogLevelToStringConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            CRLogLevel.Debug => "DBG",
            CRLogLevel.Info => "INF",
            CRLogLevel.Warning => "WRN",
            CRLogLevel.Error => "ERR",
            _ => "???",
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// bool反転コンバーター
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public static InverseBoolConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>
/// null/empty → IsVisible コンバーター
/// </summary>
public sealed class NotNullOrEmptyConverter : IValueConverter
{
    public static NotNullOrEmptyConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            string s => !string.IsNullOrEmpty(s),
            _ => true,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// OptionKind == parameter → IsVisible コンバーター
/// </summary>
public sealed class OptionKindEqualsConverter : IValueConverter
{
    public static OptionKindEqualsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is OptionKind kind && parameter is string kindName)
        {
            return Enum.TryParse<OptionKind>(kindName, out var target) && kind == target;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// カウント > 0 → IsVisible コンバーター
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public static CountToVisibilityConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count && count > 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// DateTimeOffset → 表示文字列 コンバーター
/// </summary>
public sealed class TimestampConverter : IValueConverter
{
    public static TimestampConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
        {
            return dto.ToString("HH:mm:ss.fff");
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
