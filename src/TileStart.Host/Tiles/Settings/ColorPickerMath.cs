using System.Globalization;
using MediaColor = System.Windows.Media.Color;

namespace TileStart.Host.Tiles.Settings;

/// <summary>
/// Colour conversions for <see cref="ColorPickerWindow"/>. Kept separate from the window so the
/// arithmetic is testable without creating WPF visuals.
/// </summary>
internal static class ColorPickerMath
{
    /// <summary>Hue in [0,360), saturation and value in [0,1].</summary>
    internal readonly record struct Hsv(double Hue, double Saturation, double Value);

    internal static Hsv ToHsv(MediaColor color)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        double hue;
        if (delta == 0)
        {
            hue = 0;
        }
        else if (max == red)
        {
            hue = 60 * (((green - blue) / delta) % 6);
        }
        else if (max == green)
        {
            hue = 60 * (((blue - red) / delta) + 2);
        }
        else
        {
            hue = 60 * (((red - green) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return new Hsv(hue, max == 0 ? 0 : delta / max, max);
    }

    internal static MediaColor ToColor(Hsv hsv)
    {
        var hue = ((hsv.Hue % 360) + 360) % 360;
        var saturation = Math.Clamp(hsv.Saturation, 0, 1);
        var value = Math.Clamp(hsv.Value, 0, 1);

        var chroma = value * saturation;
        var secondary = chroma * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var match = value - chroma;

        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, secondary, 0d),
            < 120 => (secondary, chroma, 0d),
            < 180 => (0d, chroma, secondary),
            < 240 => (0d, secondary, chroma),
            < 300 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary),
        };

        return MediaColor.FromRgb(
            (byte)Math.Round((red + match) * 255),
            (byte)Math.Round((green + match) * 255),
            (byte)Math.Round((blue + match) * 255));
    }

    /// <summary>Fully saturated colour for a hue, used to drive the spectrum gradient.</summary>
    internal static MediaColor HueColor(double hue) => ToColor(new Hsv(hue, 1, 1));

    internal static string ToHex(MediaColor color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>Accepts <c>#RGB</c> and <c>#RRGGBB</c>, with or without the leading hash.</summary>
    internal static bool TryParseHex(string? text, out MediaColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim().TrimStart('#');
        if (value.Length == 3)
        {
            value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
        }

        if (value.Length != 6
            || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return false;
        }

        color = MediaColor.FromRgb(
            (byte)(packed >> 16),
            (byte)(packed >> 8),
            (byte)packed);
        return true;
    }
}
