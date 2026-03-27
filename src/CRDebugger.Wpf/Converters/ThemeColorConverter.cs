using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CRDebugger.Wpf.Converters;

/// <summary>
/// uint ARGB 値を WPF の SolidColorBrush に変換する IValueConverter 実装。
/// XAML バインディングでテーマカラーをブラシに変換する際に使用する。
/// </summary>
public sealed class ThemeColorConverter : IValueConverter
{
    /// <summary>
    /// シングルトンインスタンス。XAML リソースとして共有して使用する。
    /// </summary>
    public static ThemeColorConverter Instance { get; } = new();

    /// <summary>
    /// uint ARGB 値を SolidColorBrush に変換する
    /// </summary>
    /// <param name="value">変換する uint ARGB カラー値</param>
    /// <param name="targetType">変換先の型（未使用）</param>
    /// <param name="parameter">コンバーターパラメーター（未使用）</param>
    /// <param name="culture">カルチャ情報（未使用）</param>
    /// <returns>対応する SolidColorBrush、変換不可の場合は Brushes.Transparent</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // uint 型の ARGB 値の場合のみ変換を実行
        if (value is uint argb)
        {
            return new SolidColorBrush(UintToColor(argb));
        }

        // 変換できない場合は透明ブラシを返す
        return Brushes.Transparent;
    }

    /// <summary>
    /// 逆変換は非サポート（SolidColorBrush から uint への変換は行わない）
    /// </summary>
    /// <exception cref="NotSupportedException">常にスローされる</exception>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// uint ARGB 値を WPF の Color 構造体に変換する
    /// </summary>
    /// <param name="argb">ARGB フォーマットの uint カラー値（上位8ビットがA、次がR、G、Bの順）</param>
    /// <returns>対応する WPF Color 構造体</returns>
    public static Color UintToColor(uint argb)
    {
        // ARGB の各チャンネルをビットシフトとマスクで抽出
        var a = (byte)((argb >> 24) & 0xFF);  // アルファチャンネル（上位8ビット）
        var r = (byte)((argb >> 16) & 0xFF);  // 赤チャンネル
        var g = (byte)((argb >> 8) & 0xFF);   // 緑チャンネル
        var b = (byte)(argb & 0xFF);           // 青チャンネル（下位8ビット）
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// uint ARGB 値をフリーズ済みの SolidColorBrush に変換する。
    /// フリーズすることでスレッドセーフかつメモリ効率が向上する。
    /// </summary>
    /// <param name="argb">ARGB フォーマットの uint カラー値</param>
    /// <returns>フリーズ済みの SolidColorBrush</returns>
    public static SolidColorBrush UintToBrush(uint argb)
    {
        var brush = new SolidColorBrush(UintToColor(argb));
        // フリーズすることで変更不可・スレッドセーフなブラシになる
        brush.Freeze();
        return brush;
    }
}
