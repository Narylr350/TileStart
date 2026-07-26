using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using TileStart.Host.Themes;
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

    // Neutral fallback used when DWM publishes no wallpaper-derived Start colour.
    // See docs/reference/win11-start/specs/theme-brushes.json (hostBackdropVariant).
    private const int Windows11StartAcrylicGradientColor = unchecked((int)0xCC1C1C1C);
    // TintOpacity 0.75 from the StartDocked HostBackdrop acrylic; leaves enough of the
    // blurred wallpaper visible instead of covering it.
    private const uint Windows11StartAcrylicTintAlpha = 0xBF;
    private static readonly MediaColor StartFallbackColor = MediaColor.FromRgb(0x1F, 0x1F, 0x1F);
    private static readonly MediaColor NeutralStartSurfaceColor = MediaColor.FromRgb(0x1C, 0x1C, 0x1C);

    public static MediaColor AccentColor { get; } = ReadAccentColor();

    public static SolidColorBrush AccentBrush { get; } = CreateFrozenBrush(AccentColor);

    public static SolidColorBrush AccentHoverBrush { get; } = CreateFrozenBrush(Blend(AccentColor, Colors.White, 0.10));

    public static SolidColorBrush AccentPressedBrush { get; } =
        CreateFrozenBrush(Blend(AccentColor, Colors.Black, 0.12));

    public static SolidColorBrush AccentForegroundBrush { get; } =
        CreateFrozenBrush(UseDarkForeground(AccentColor) ? Colors.Black : Colors.White);

    public static SolidColorBrush ContextMenuHighlightBrush => AccentBrush;

    /// <summary>
    /// Wallpaper-derived Start colour DWM publishes for the Start menu, or a neutral dark
    /// tone when "automatically pick an accent colour from my background" is off. Used by the
    /// Windows 11 theme for surfaces that sit over the acrylic and must share its hue.
    /// </summary>
    public static MediaColor StartSurfaceColor { get; } = ReadStartSurfaceColor();

    /// <summary>
    /// Windows 11 navigation overlay: the wallpaper-derived Start colour at the same opacity
    /// the acrylic tint uses, so expanding the pane deepens the surface instead of covering it
    /// with a flat grey.
    /// </summary>
    public static MediaColor StartSurfaceOverlayColor { get; } =
        MediaColor.FromArgb(0xF2, StartSurfaceColor.R, StartSurfaceColor.G, StartSurfaceColor.B);

    /// <summary>
    /// Windows 11 tile face: the wallpaper-derived Start colour darkened so tiles stay readable
    /// while still belonging to the surrounding surface.
    /// </summary>
    public static MediaColor StartTileBackgroundColor { get; } =
        Blend(StartSurfaceColor, Colors.Black, 0.32);

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
        => ResolveStartMaterial(enableTransparency, highContrast, AppThemeStyle.Windows10);

    internal static StartMaterialConfiguration ResolveStartMaterial(
        object? enableTransparency,
        bool highContrast,
        AppThemeStyle themeStyle)
        => ResolveStartMaterial(enableTransparency, highContrast, themeStyle, startColorMenu: null);

    internal static StartMaterialConfiguration ResolveStartMaterial(
        object? enableTransparency,
        bool highContrast,
        AppThemeStyle themeStyle,
        object? startColorMenu)
    {
        var transparencyEnabled = enableTransparency is int value && value != 0;
        return new StartMaterialConfiguration(
            transparencyEnabled && !highContrast,
            StartFallbackColor,
            themeStyle == AppThemeStyle.Windows11
                ? ResolveWindows11GradientColor(startColorMenu)
                : StartAcrylicGradientColor);
    }

    /// <summary>
    /// Windows 11 tints the Start surface with the wallpaper-derived colour DWM publishes as
    /// StartColorMenu, so the menu picks up the desktop hue instead of a fixed grey. Falls back
    /// to a neutral dark tint when the value is missing, which is the case when
    /// "automatically pick an accent colour from my background" is off.
    /// </summary>
    internal static int ResolveWindows11GradientColor(object? startColorMenu)
    {
        if (!TryReadPackedColor(startColorMenu, out var packed))
        {
            return Windows11StartAcrylicGradientColor;
        }

        // StartColorMenu stores the colour as 0x00BBGGRR; AccentPolicy expects 0xAABBGGRR,
        // so the existing tint's alpha is kept and only the colour channels are replaced.
        return unchecked((int)((Windows11StartAcrylicTintAlpha << 24) | (packed & 0x00FFFFFF)));
    }

    internal static StartMaterialConfiguration ReadStartMaterial(AppThemeStyle themeStyle)
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

        object? startColorMenu = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentRegistryPath);
            startColorMenu = key?.GetValue("StartColorMenu");
        }
        catch (Exception)
        {
        }

        return ResolveStartMaterial(
            enableTransparency,
            SystemParameters.HighContrast,
            themeStyle,
            startColorMenu);
    }

    private static MediaColor ReadStartSurfaceColor()
    {
        object? startColorMenu = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentRegistryPath);
            startColorMenu = key?.GetValue("StartColorMenu");
        }
        catch (Exception)
        {
        }

        return ResolveStartSurfaceColor(startColorMenu);
    }

    internal static MediaColor ResolveStartSurfaceColor(object? startColorMenu)
    {
        // StartColorMenu is packed as 0x00BBGGRR.
        return TryReadPackedColor(startColorMenu, out var packed)
            ? MediaColor.FromRgb((byte)packed, (byte)(packed >> 8), (byte)(packed >> 16))
            : NeutralStartSurfaceColor;
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
        if (!TryReadPackedColor(value, out var packed))
        {
            color = default;
            return false;
        }

        color = MediaColor.FromRgb(
            (byte)packed,
            (byte)(packed >> 8),
            (byte)(packed >> 16));
        return true;
    }

    private static bool TryReadPackedColor(object? value, out uint packed)
    {
        switch (value)
        {
            case int signed:
                packed = unchecked((uint)signed);
                return true;
            case uint unsigned:
                packed = unsigned;
                return true;
            default:
                packed = 0;
                return false;
        }
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