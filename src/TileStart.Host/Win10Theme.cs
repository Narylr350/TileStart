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
    private const int DarkAccentPaletteOffset = 6 * 4;
    private const int StartAcrylicGradientColor = unchecked((int)0xBF101010);
    private static readonly MediaColor StartFallbackColor = MediaColor.FromRgb(0x1F, 0x1F, 0x1F);

    public static SolidColorBrush ContextMenuHighlightBrush { get; } = CreateContextMenuHighlightBrush();

    internal static MediaColor ReadDarkAccent(byte[]? palette, MediaColor fallback)
    {
        return palette is { Length: >= DarkAccentPaletteOffset + 3 }
            ? MediaColor.FromRgb(palette[DarkAccentPaletteOffset], palette[DarkAccentPaletteOffset + 1],
                palette[DarkAccentPaletteOffset + 2])
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

    private static SolidColorBrush CreateContextMenuHighlightBrush()
    {
        var fallback = SystemParameters.WindowGlassColor;
        byte[]? palette = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentRegistryPath);
            palette = key?.GetValue("AccentPalette") as byte[];
        }
        catch (Exception)
        {
        }

        var brush = new SolidColorBrush(ReadDarkAccent(palette, fallback));
        brush.Freeze();
        return brush;
    }
}
