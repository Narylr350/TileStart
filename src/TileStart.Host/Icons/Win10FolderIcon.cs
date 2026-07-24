using System.Windows;
using System.Windows.Media;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaPen = System.Windows.Media.Pen;
using WindowsPoint = System.Windows.Point;

namespace TileStart.Host.Icons;

internal static class Win10FolderIcon
{
    public static ImageSource Image { get; } = Create();

    private static ImageSource Create()
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(MediaBrushes.Transparent, null,
            new RectangleGeometry(new Rect(0, 0, 24, 24))));

        var back = new LinearGradientBrush(
            MediaColor.FromRgb(0xFF, 0xC7, 0x2E),
            MediaColor.FromRgb(0xE5, 0x9A, 0x00),
            new WindowsPoint(0, 0), new WindowsPoint(0, 1));
        back.Freeze();
        drawing.Children.Add(new GeometryDrawing(back, null, Geometry.Parse(
            "M 1.5,10 L 1.5,5.5 Q 1.5,3.5 3.5,3.5 L 9.5,3.5 Q 10.5,3.5 11.3,4.3 L 14,7 L 20.5,7 Q 22.5,7 22.5,9 L 22.5,11 Z")));

        var front = new LinearGradientBrush(
            MediaColor.FromRgb(0xFF, 0xDC, 0x78),
            MediaColor.FromRgb(0xFF, 0xC6, 0x35),
            new WindowsPoint(0, 0), new WindowsPoint(0, 1));
        front.Freeze();
        var outline = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0x38, 0xA0, 0x6A, 0x00)), 0.5);
        outline.Freeze();
        drawing.Children.Add(new GeometryDrawing(front, outline, Geometry.Parse(
            "M 3,7.5 L 21,7.5 Q 22.5,7.5 22.5,9.3 L 22.5,19 Q 22.5,21 20.5,21 L 3.5,21 Q 1.5,21 1.5,19 L 1.5,9.3 Q 1.5,7.5 3,7.5 Z")));

        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}