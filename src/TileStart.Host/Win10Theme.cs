using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace TileStart.Host;

public static class Win10Theme
{
    private const string AccentRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const int DarkAccentPaletteOffset = 6 * 4;

    public static SolidColorBrush ContextMenuHighlightBrush { get; } = CreateContextMenuHighlightBrush();

    internal static MediaColor ReadDarkAccent(byte[]? palette, MediaColor fallback)
    {
        return palette is { Length: >= DarkAccentPaletteOffset + 3 }
            ? MediaColor.FromRgb(palette[DarkAccentPaletteOffset], palette[DarkAccentPaletteOffset + 1], palette[DarkAccentPaletteOffset + 2])
            : fallback;
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
