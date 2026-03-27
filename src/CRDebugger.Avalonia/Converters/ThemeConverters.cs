using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CRDebugger.Core;
using CRDebugger.Core.Logging;
using CRDebugger.Core.Options;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.Avalonia.Converters;

/// <summary>
/// ARGB の uint 値を <see cref="SolidColorBrush"/> に変換するコンバーター。
/// ViewModel のカラープロパティを Avalonia の描画ブラシにバインドする際に使用する。
/// </summary>
public sealed class UIntToColorBrushConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static UIntToColorBrushConverter Instance { get; } = new();

    /// <summary>
    /// uint の ARGB 値を <see cref="SolidColorBrush"/> に変換する。
    /// 変換できない場合は透明ブラシを返す。
    /// </summary>
    /// <param name="value">変換元の uint ARGB 値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>対応する <see cref="SolidColorBrush"/>、変換失敗時は透明ブラシ</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is uint argb)
        {
            // uint を Avalonia の Color 構造体に変換する
            var color = Color.FromUInt32(argb);
            return new SolidColorBrush(color);
        }
        return Brushes.Transparent;
    }

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// <see cref="CRTab"/> 列挙値とパラメーターが一致するか bool で返すコンバーター。
/// タブボタンの選択状態バインディングに使用する。
/// </summary>
public sealed class TabEqualsConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static TabEqualsConverter Instance { get; } = new();

    /// <summary>
    /// 現在選択中のタブとパラメーターのタブ名が一致するか bool で返す。
    /// </summary>
    /// <param name="value">現在の <see cref="CRTab"/> 値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">比較対象のタブ名文字列</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>タブが一致すれば true、それ以外は false</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CRTab tab && parameter is string tabName)
        {
            // パラメーターの文字列を CRTab 列挙値にパースして比較する
            return Enum.TryParse<CRTab>(tabName, out var target) && tab == target;
        }
        return false;
    }

    /// <summary>
    /// bool から <see cref="CRTab"/> に逆変換する。true の場合はパラメーターのタブを返す。
    /// </summary>
    /// <param name="value">bool 値（true なら対応タブを返す）</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">対応するタブ名文字列</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>対応する <see cref="CRTab"/>、変換失敗時は Console タブ</returns>
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
/// <see cref="CRLogLevel"/> を対応する表示色の <see cref="SolidColorBrush"/> に変換するコンバーター。
/// ログエントリの色分け表示に使用する。
/// </summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static LogLevelToBrushConverter Instance { get; } = new();

    /// <summary>Debug レベルの表示色（青系）</summary>
    private static readonly SolidColorBrush DebugBrush = new(Color.FromUInt32(0xFF6CAEDD));
    /// <summary>Info レベルの表示色（グレー）</summary>
    private static readonly SolidColorBrush InfoBrush = new(Color.FromUInt32(0xFFB0B0B0));
    /// <summary>Warning レベルの表示色（黄色系）</summary>
    private static readonly SolidColorBrush WarningBrush = new(Color.FromUInt32(0xFFE8C44A));
    /// <summary>Error レベルの表示色（赤系）</summary>
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromUInt32(0xFFE05252));

    /// <summary>
    /// <see cref="CRLogLevel"/> を対応するブラシに変換する。
    /// 未知のレベルは Info と同じグレーを返す。
    /// </summary>
    /// <param name="value">変換元の <see cref="CRLogLevel"/> 値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>ログレベルに対応する <see cref="SolidColorBrush"/></returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            CRLogLevel.Debug => DebugBrush,
            CRLogLevel.Info => InfoBrush,
            CRLogLevel.Warning => WarningBrush,
            CRLogLevel.Error => ErrorBrush,
            // 未知のレベルはデフォルトで Info ブラシを使用する
            _ => InfoBrush,
        };
    }

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// <see cref="CRLogLevel"/> を短縮文字列（"DBG" / "INF" / "WRN" / "ERR"）に変換するコンバーター。
/// ログ一覧のレベル列表示に使用する。
/// </summary>
public sealed class LogLevelToStringConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static LogLevelToStringConverter Instance { get; } = new();

    /// <summary>
    /// <see cref="CRLogLevel"/> を3文字の短縮文字列に変換する。
    /// 未知のレベルは "???" を返す。
    /// </summary>
    /// <param name="value">変換元の <see cref="CRLogLevel"/> 値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>3文字の短縮レベル文字列</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            CRLogLevel.Debug => "DBG",
            CRLogLevel.Info => "INF",
            CRLogLevel.Warning => "WRN",
            CRLogLevel.Error => "ERR",
            // 未知のレベルは "???" を返す
            _ => "???",
        };
    }

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// bool 値を反転するコンバーター。true → false、false → true に変換する。
/// IsEnabled や IsVisible の否定バインディングに使用する。
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static InverseBoolConverter Instance { get; } = new();

    /// <summary>
    /// bool を反転して返す。bool 以外の値はそのまま返す。
    /// </summary>
    /// <param name="value">変換元の値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>反転した bool 値、または元の値</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    /// <summary>
    /// 逆変換も同様に bool を反転する。
    /// </summary>
    /// <param name="value">変換元の値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>反転した bool 値、または元の値</returns>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>
/// null または空文字列の場合に false、それ以外は true を返すコンバーター。
/// IsVisible バインディングで値が存在する場合のみ表示するために使用する。
/// </summary>
public sealed class NotNullOrEmptyConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static NotNullOrEmptyConverter Instance { get; } = new();

    /// <summary>
    /// 値が null または空文字列でない場合に true を返す。
    /// </summary>
    /// <param name="value">判定対象の値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>値が存在する場合は true、null または空文字列の場合は false</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            // 文字列の場合は空文字列チェックも行う
            string s => !string.IsNullOrEmpty(s),
            // それ以外のオブジェクトは null でないので true を返す
            _ => true,
        };
    }

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// <see cref="OptionKind"/> 列挙値とパラメーターが一致するか bool で返すコンバーター。
/// オプション種別に応じた UI 要素の表示切り替えに使用する。
/// </summary>
public sealed class OptionKindEqualsConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static OptionKindEqualsConverter Instance { get; } = new();

    /// <summary>
    /// オプション種別がパラメーターと一致するか bool で返す。
    /// </summary>
    /// <param name="value">現在の <see cref="OptionKind"/> 値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">比較対象の OptionKind 名文字列</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>OptionKind が一致すれば true、それ以外は false</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is OptionKind kind && parameter is string kindName)
        {
            // パラメーターの文字列を OptionKind 列挙値にパースして比較する
            return Enum.TryParse<OptionKind>(kindName, out var target) && kind == target;
        }
        return false;
    }

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// int 値が 0 より大きい場合に true を返すコンバーター。
/// コレクションのカウントが 0 でない場合のみ UI 要素を表示する IsVisible バインディングに使用する。
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static CountToVisibilityConverter Instance { get; } = new();

    /// <summary>
    /// int 値が 0 より大きい場合に true を返す。
    /// </summary>
    /// <param name="value">判定対象の int 値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>count > 0 なら true、それ以外は false</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // int にキャストして 0 より大きいか判定する
        return value is int count && count > 0;
    }

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 値が <see cref="ActionItemViewModel"/> のインスタンスかどうかを bool で返すコンバーター。
/// アクションアイテムの DataTemplate 切り替えに使用する。
/// </summary>
public sealed class IsActionItemConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static IsActionItemConverter Instance { get; } = new();

    /// <summary>
    /// 値が <see cref="ActionItemViewModel"/> のインスタンスであれば true を返す。
    /// </summary>
    /// <param name="value">判定対象の値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>ActionItemViewModel なら true</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ActionItemViewModel;

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 値が <see cref="ActionItemViewModel"/> のインスタンスでないかどうかを bool で返すコンバーター。
/// アクションアイテム以外の DataTemplate 切り替えに使用する。
/// </summary>
public sealed class IsNotActionItemConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static IsNotActionItemConverter Instance { get; } = new();

    /// <summary>
    /// 値が <see cref="ActionItemViewModel"/> のインスタンスでなければ true を返す。
    /// </summary>
    /// <param name="value">判定対象の値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>ActionItemViewModel でなければ true</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not ActionItemViewModel;

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// <see cref="DateTimeOffset"/> を "HH:mm:ss.fff" 形式の文字列に変換するコンバーター。
/// ログエントリのタイムスタンプ表示に使用する。
/// </summary>
public sealed class TimestampConverter : IValueConverter
{
    /// <summary>シングルトンインスタンス。XAML から参照する際に使用する</summary>
    public static TimestampConverter Instance { get; } = new();

    /// <summary>
    /// <see cref="DateTimeOffset"/> を時刻文字列（"HH:mm:ss.fff"）に変換する。
    /// <see cref="DateTimeOffset"/> 以外の値は空文字列を返す。
    /// </summary>
    /// <param name="value">変換元の <see cref="DateTimeOffset"/> 値</param>
    /// <param name="targetType">バインディングのターゲット型</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャー情報（未使用）</param>
    /// <returns>"HH:mm:ss.fff" 形式の文字列、変換失敗時は空文字列</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
        {
            // ミリ秒まで含む時刻文字列にフォーマットする
            return dto.ToString("HH:mm:ss.fff");
        }
        return string.Empty;
    }

    /// <summary>逆変換は非サポート。呼び出すと例外をスローする</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
