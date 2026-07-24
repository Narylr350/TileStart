using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.IO;
using System.Windows.Media;
using TileStart.Host.Utilities;

namespace TileStart.Host.Icons;

public static class SvgIconLoader
{
    private static readonly WpfDrawingSettings DrawingSettings = new()
    {
        IncludeRuntime = false,
        TextAsGeometry = false,
    };

    public static ImageSource? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var reader = File.OpenText(path);
            return Load(reader);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"SVG icon load failed for '{path}': {exception.Message}");
            return null;
        }
    }

    public static ImageSource? LoadText(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > CustomIconStore.MaximumSvgLength)
        {
            return null;
        }

        try
        {
            using var reader = new StringReader(source);
            return Load(reader);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"SVG icon parse failed: {exception.Message}");
            return null;
        }
    }

    private static ImageSource? Load(TextReader source)
    {
        var converter = new FileSvgReader(DrawingSettings);
        var drawing = converter.Read(source);
        if (drawing is null)
        {
            return null;
        }

        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
