using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TileStart.Host;

public sealed class IsGifPathConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path
        && path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
        && File.Exists(path);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
