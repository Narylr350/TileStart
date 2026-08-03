using System.Windows;
using System.Windows.Media;

namespace TileStart.Host;

/// <summary>
/// 按主窗口坐标连续平铺图片，而不是在每个磁贴内部重新从纹理左上角开始。
/// 原版 Acrylic 噪声属于同一合成表面；若每块磁贴重复同一相位，会形成明显的复制粘贴纹理。
/// </summary>
public sealed class WindowAlignedImageTile : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(WindowAlignedImageTile),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var source = Source;
        if (source is null || source.Width <= 0 || source.Height <= 0
            || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var window = Window.GetWindow(this);
        var origin = window is null
            ? new System.Windows.Point()
            : TranslatePoint(new System.Windows.Point(), window);
        var startX = -PositiveModulo(origin.X, source.Width);
        var startY = -PositiveModulo(origin.Y, source.Height);

        for (var y = startY; y < ActualHeight; y += source.Height)
        {
            for (var x = startX; x < ActualWidth; x += source.Width)
            {
                drawingContext.DrawImage(source, new Rect(x, y, source.Width, source.Height));
            }
        }
    }

    private static double PositiveModulo(double value, double divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}
