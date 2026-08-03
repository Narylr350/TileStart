using System.Collections;
using System.IO;
using System.Resources;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileStart.Host.Icons;

internal static class TileStartAppIcon
{
    public static ImageSource Image { get; } = Load();

    private static ImageSource Load()
    {
        const string resourceKey = "assets/tilestart-icon-master.png";
        var assembly = typeof(TileStartAppIcon).Assembly;
        var resourceName = $"{assembly.GetName().Name}.g.resources";
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
                                   ?? throw new InvalidOperationException($"Missing WPF resources: {resourceName}");
        using var reader = new ResourceReader(resourceStream);
        var iconStream = reader
            .Cast<DictionaryEntry>()
            .Where(entry => entry.Key is string key && key.Equals(resourceKey, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Value as Stream)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Missing TileStart icon resource: {resourceKey}");

        var image = BitmapFrame.Create(
            iconStream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        image.Freeze();
        return image;
    }
}
