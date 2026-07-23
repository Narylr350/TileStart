using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileStart.Host;

internal static class IconImageNormalizer
{
    public static ImageSource? NormalizeShellIcon(ImageSource? source)
    {
        if (source is not BitmapSource bitmap || bitmap.PixelWidth <= 1 || bitmap.PixelHeight <= 1)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var left = converted.PixelWidth;
        var top = converted.PixelHeight;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < converted.PixelHeight; y++)
        {
            for (var x = 0; x < converted.PixelWidth; x++)
            {
                if (pixels[y * stride + x * 4 + 3] <= 8)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left || bottom < top)
        {
            return null;
        }

        var width = right - left + 1;
        var height = bottom - top + 1;
        if (width >= converted.PixelWidth * 0.8 && height >= converted.PixelHeight * 0.8)
        {
            return source;
        }

        var cropped = new CroppedBitmap(converted, new System.Windows.Int32Rect(left, top, width, height));
        cropped.Freeze();
        return cropped;
    }
}