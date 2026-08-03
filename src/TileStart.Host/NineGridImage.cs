using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileStart.Host;

/// <summary>
/// 绘制 WinUI Image.NineGrid 的最小兼容层。
/// 原版导航阴影不是模糊算法，而是 53×53 的九宫格 PNG；保持边角和边缘像素不拉伸，
/// 否则展开导航栏变宽时阴影会被压扁，右侧会出现明显的灰带。
/// </summary>
public sealed class NineGridImage : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(NineGridImage),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

    public static readonly DependencyProperty NineGridProperty = DependencyProperty.Register(
        nameof(NineGrid),
        typeof(double),
        typeof(NineGridImage),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private BitmapSource? _bitmap;

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double NineGrid
    {
        get => (double)GetValue(NineGridProperty);
        set => SetValue(NineGridProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var bitmap = _bitmap;
        if (bitmap is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var left = Math.Min(NineGrid, bitmap.PixelWidth / 2d);
        var right = left;
        var top = Math.Min(NineGrid, bitmap.PixelHeight / 2d);
        var bottom = top;
        var sourceCenterWidth = bitmap.PixelWidth - left - right;
        var sourceCenterHeight = bitmap.PixelHeight - top - bottom;
        if (sourceCenterWidth <= 0 || sourceCenterHeight <= 0)
        {
            return;
        }

        var destinationLeft = Math.Min(left, ActualWidth / 2d);
        var destinationRight = Math.Min(right, ActualWidth - destinationLeft);
        var destinationTop = Math.Min(top, ActualHeight / 2d);
        var destinationBottom = Math.Min(bottom, ActualHeight - destinationTop);
        var destinationCenterWidth = Math.Max(0, ActualWidth - destinationLeft - destinationRight);
        var destinationCenterHeight = Math.Max(0, ActualHeight - destinationTop - destinationBottom);

        var sourceX = new[] { 0d, left, left + sourceCenterWidth };
        var sourceY = new[] { 0d, top, top + sourceCenterHeight };
        var sourceWidth = new[] { left, sourceCenterWidth, right };
        var sourceHeight = new[] { top, sourceCenterHeight, bottom };
        var destinationX = new[] { 0d, destinationLeft, destinationLeft + destinationCenterWidth };
        var destinationY = new[] { 0d, destinationTop, destinationTop + destinationCenterHeight };
        var destinationWidth = new[] { destinationLeft, destinationCenterWidth, destinationRight };
        var destinationHeight = new[] { destinationTop, destinationCenterHeight, destinationBottom };

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (sourceWidth[column] <= 0 || sourceHeight[row] <= 0
                    || destinationWidth[column] <= 0 || destinationHeight[row] <= 0)
                {
                    continue;
                }

                var sourceRect = new Int32Rect(
                    (int)Math.Round(sourceX[column]),
                    (int)Math.Round(sourceY[row]),
                    Math.Max(1, (int)Math.Round(sourceWidth[column])),
                    Math.Max(1, (int)Math.Round(sourceHeight[row])));
                var cropped = new CroppedBitmap(bitmap, sourceRect);
                drawingContext.DrawImage(
                    cropped,
                    new Rect(
                        destinationX[column],
                        destinationY[row],
                        destinationWidth[column],
                        destinationHeight[row]));
            }
        }
    }

    private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (NineGridImage)dependencyObject;
        control._bitmap = args.NewValue as BitmapSource;
        control.InvalidateVisual();
    }
}
