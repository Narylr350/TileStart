using System.Globalization;
using System.Windows.Data;

namespace TileStart.Host.Icons;

public sealed class NonGifImageSourceConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var source = values.Length > 0 ? values[0] : null;
        var path = values.Length > 1 ? values[1] as string : null;
        return path?.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) == true ? null : source;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
