using System.Windows;
using System.Windows.Media;

namespace TileStart.Host.Icons;

internal static class GenericAppIcon
{
    public static ImageSource Image { get; } = Create();

    private static ImageSource Create()
    {
        var background = new GeometryDrawing(
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x46, 0x72, 0x96)),
            null,
            new RectangleGeometry(new Rect(0, 0, 32, 32), 3, 3));
        var foreground = new GeometryDrawing(
            System.Windows.Media.Brushes.White,
            null,
            Geometry.Parse("M6,7 H14 V15 H6 Z M18,7 H26 V15 H18 Z M6,19 H14 V27 H6 Z M18,19 H26 V27 H18 Z"));
        var group = new DrawingGroup { Children = { background, foreground } };
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}