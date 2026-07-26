using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace TileStart.Host;

internal readonly record struct StartMaterialConfiguration(
    bool UseAcrylic,
    MediaColor FallbackColor,
    int AcrylicGradientColor);

public static class Win10Theme
{
    private const string AccentRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int AccentPaletteOffset = 3 * 4;
    private const int StartAcrylicGradientColor = unchecked((int)0xBF101010);
    private static readonly MediaColor StartFallbackColor = MediaColor.FromRgb(0x1F, 0x1F, 0x1F);

    public static MediaColor AccentColor { get; } = ReadAccentColor();

    public static SolidColorBrush AccentBrush { get; } = CreateFrozenBrush(AccentColor);

    public static SolidColorBrush AccentHoverBrush { get; } = CreateFrozenBrush(Blend(AccentColor, Colors.White, 0.10));

    public static SolidColorBrush AccentPressedBrush { get; } = CreateFrozenBrush(Blend(AccentColor, Colors.Black, 0.12));

    public static SolidColorBrush AccentForegroundBrush { get; } =
        CreateFrozenBrush(UseDarkForeground(AccentColor) ? Colors.Black : Colors.White);

    public static SolidColorBrush ContextMenuHighlightBrush => AccentBrush;

    internal static MediaColor ResolveAccentColor(object? accentColorMenu, byte[]? palette, MediaColor fallback)
    {
        if (TryReadAccentColorMenu(accentColorMenu, out var color))
        {
            return color;
        }

        return palette is { Length: >= AccentPaletteOffset + 3 }
            ? MediaColor.FromRgb(palette[AccentPaletteOffset], palette[AccentPaletteOffset + 1],
                palette[AccentPaletteOffset + 2])
            : fallback;
    }

    internal static StartMaterialConfiguration ResolveStartMaterial(object? enableTransparency, bool highContrast)
    {
        var transparencyEnabled = enableTransparency is int value && value != 0;
        return new StartMaterialConfiguration(
            transparencyEnabled && !highContrast,
            StartFallbackColor,
            StartAcrylicGradientColor);
    }

    internal static StartMaterialConfiguration ReadStartMaterial()
    {
        object? enableTransparency = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            enableTransparency = key?.GetValue("EnableTransparency");
        }
        catch (Exception)
        {
        }

        return ResolveStartMaterial(enableTransparency, SystemParameters.HighContrast);
    }

    private static MediaColor ReadAccentColor()
    {
        var fallback = SystemParameters.WindowGlassColor;
        object? accentColorMenu = null;
        byte[]? palette = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentRegistryPath);
            accentColorMenu = key?.GetValue("AccentColorMenu");
            palette = key?.GetValue("AccentPalette") as byte[];
        }
        catch (Exception)
        {
        }

        return ResolveAccentColor(accentColorMenu, palette, fallback);
    }

    private static bool TryReadAccentColorMenu(object? value, out MediaColor color)
    {
        uint packed;
        switch (value)
        {
            case int signed:
                packed = unchecked((uint)signed);
                break;
            case uint unsigned:
                packed = unsigned;
                break;
            default:
                color = default;
                return false;
        }

        color = MediaColor.FromRgb(
            (byte)packed,
            (byte)(packed >> 8),
            (byte)(packed >> 16));
        return true;
    }

    internal static MediaColor Blend(MediaColor source, MediaColor target, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return MediaColor.FromRgb(
            (byte)Math.Round(source.R + ((target.R - source.R) * amount)),
            (byte)Math.Round(source.G + ((target.G - source.G) * amount)),
            (byte)Math.Round(source.B + ((target.B - source.B) * amount)));
    }

    internal static bool UseDarkForeground(MediaColor background)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var luminance = (0.2126 * Linearize(background.R))
                        + (0.7152 * Linearize(background.G))
                        + (0.0722 * Linearize(background.B));
        return luminance > 0.44;
    }

    private static SolidColorBrush CreateFrozenBrush(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}