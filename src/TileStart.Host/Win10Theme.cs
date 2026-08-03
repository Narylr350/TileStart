using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using TileStart.Host.Themes;
using MediaColor = System.Windows.Media.Color;

namespace TileStart.Host;

internal readonly record struct StartMaterialConfiguration(
    bool UseAcrylic,
    MediaColor FallbackColor,
    int AcrylicGradientColor,
    MediaColor LiveResizeColor);

public static class Win10Theme
{
    private const string AccentRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int AccentPaletteOffset = 3 * 4;
    private const int StartAcrylicGradientColor = unchecked((int)0xBF101010);
    // 原版 UWP TintOpacity=0.8 不能直接等同于 WCA GradientColor alpha；0xCC 会把
    // 壁纸压成均匀的高饱和蓝板。0xB8 是同壁纸对照 Win10 原生截图后的兼容校准值。
    private const uint Win10AccentAcrylicTintAlpha = 0xB8;
    // WCA 缺少 UWP Acrylic 的 luminosity 合成。修正 Dark1 来源后，同壁纸 ROI 表明 16% 会明显压暗；
    // 7% 是当前跨原生/复刻截图的兼容拟合值，不是 StartUI 内部公开参数。
    private const double Win10AccentAcrylicNeutralBlend = 0.07;
    // Win10 19045 同一强调色样本中，Dark2 相当于先降低约 15% value，再把 HSV saturation
    // 放大约 1.26 倍；该兼容派生用于 NavigationPane 的独立 0.5 tint，不能读取宿主 Win11 Palette[5]。
    private const double Win10Dark2DarkenAmount = 0.15;
    private const double Win10Dark2SaturationScale = 1.26;
    private const byte Win10NavigationOverlayAlpha = 0x80;

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

    public static MediaColor NavigationOverlayColor { get; } = ReadNavigationOverlayColor();

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
        => ResolveStartMaterial(
            enableTransparency,
            highContrast,
            themeStyle,
            startColorMenu,
            colorPrevalence: null,
            accentPalette: null);

    internal static StartMaterialConfiguration ResolveStartMaterial(
        object? enableTransparency,
        bool highContrast,
        AppThemeStyle themeStyle,
        object? startColorMenu,
        object? colorPrevalence,
        byte[]? accentPalette,
        object? accentColorMenu = null)
    {
        var transparencyEnabled = enableTransparency is int value && value != 0;
        var useWin10AccentAcrylic = themeStyle == AppThemeStyle.Windows10
                                    && colorPrevalence is int prevalence
                                    && prevalence != 0;
        var win10AccentColor = useWin10AccentAcrylic
            ? ResolveWin10AccentAcrylicColor(accentColorMenu, startColorMenu, accentPalette)
            : StartFallbackColor;

        var useAcrylic = transparencyEnabled && !highContrast;
        var fallbackColor = useWin10AccentAcrylic && !highContrast ? win10AccentColor : StartFallbackColor;
        var liveResizeColor = useAcrylic && themeStyle == AppThemeStyle.Windows11
            ? ResolveStartSurfaceColor(startColorMenu)
            : fallbackColor;

        return new StartMaterialConfiguration(
            useAcrylic,
            fallbackColor,
            themeStyle == AppThemeStyle.Windows11
                ? ResolveWindows11GradientColor(startColorMenu)
                : useWin10AccentAcrylic
                    ? PackAccentPolicyColor(
                        ResolveWin10AccentWcaTintColor(win10AccentColor),
                        Win10AccentAcrylicTintAlpha)
                    : StartAcrylicGradientColor,
            liveResizeColor);
    }

    internal static MediaColor ResolveWin10AccentWcaTintColor(MediaColor accentDark1) =>
        Blend(accentDark1, StartFallbackColor, Win10AccentAcrylicNeutralBlend);

    /// <summary>
    /// StartUI 的 AccentAcrylic 状态使用 SystemAccentColorDark1。宿主 Win11 的 Palette[4] 与 Win10
    /// Dark1 不同，不能直接复用；当前由 AccentColorMenu/Palette[3] 按实测 90% 通道比例派生。
    /// 该比例来自同一强调色的 Win10/Win11 注册表证据，后续取得更多色样后再替换为更完整的算法。
    /// </summary>
    internal static MediaColor ResolveWin10AccentAcrylicColor(
        object? accentColorMenu,
        object? startColorMenu,
        byte[]? accentPalette)
    {
        if (TryReadAccentColorMenu(accentColorMenu, out var accent))
        {
            return DeriveWin10Dark1(accent);
        }

        if (accentPalette is { Length: >= AccentPaletteOffset + 3 })
        {
            return DeriveWin10Dark1(MediaColor.FromRgb(
                accentPalette[AccentPaletteOffset],
                accentPalette[AccentPaletteOffset + 1],
                accentPalette[AccentPaletteOffset + 2]));
        }

        // 旧系统或损坏配置可能只留下 StartColorMenu。此值已经是 Start 使用色，无法确认其基础强调色时
        // 不再二次压暗，避免把真实 Win10 Dark1 再乘一次 0.9。
        return TryReadPackedColor(startColorMenu, out var packed)
            ? MediaColor.FromRgb((byte)packed, (byte)(packed >> 8), (byte)(packed >> 16))
            : StartFallbackColor;
    }

    internal static MediaColor DeriveWin10Dark1(MediaColor accentColor) =>
        Blend(accentColor, Colors.Black, 0.10);

    internal static MediaColor DeriveWin10Dark2(MediaColor accentColor) =>
        ScaleSaturation(
            Blend(accentColor, Colors.Black, Win10Dark2DarkenAmount),
            Win10Dark2SaturationScale);

    internal static MediaColor ResolveWin10NavigationOverlayColor(
        object? colorPrevalence,
        object? accentColorMenu,
        byte[]? accentPalette,
        MediaColor neutralOverlay)
    {
        if (colorPrevalence is not int prevalence || prevalence == 0)
        {
            return neutralOverlay;
        }

        MediaColor accent;
        if (!TryReadAccentColorMenu(accentColorMenu, out accent))
        {
            if (accentPalette is not { Length: >= AccentPaletteOffset + 3 })
            {
                return neutralOverlay;
            }

            accent = MediaColor.FromRgb(
                accentPalette[AccentPaletteOffset],
                accentPalette[AccentPaletteOffset + 1],
                accentPalette[AccentPaletteOffset + 2]);
        }

        var dark2 = DeriveWin10Dark2(accent);
        return MediaColor.FromArgb(Win10NavigationOverlayAlpha, dark2.R, dark2.G, dark2.B);
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
        object? colorPrevalence = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            enableTransparency = key?.GetValue("EnableTransparency");
            colorPrevalence = key?.GetValue("ColorPrevalence");
        }
        catch (Exception)
        {
        }

        object? startColorMenu = null;
        object? accentColorMenu = null;
        byte[]? accentPalette = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentRegistryPath);
            startColorMenu = key?.GetValue("StartColorMenu");
            accentColorMenu = key?.GetValue("AccentColorMenu");
            accentPalette = key?.GetValue("AccentPalette") as byte[];
        }
        catch (Exception)
        {
        }

        return ResolveStartMaterial(
            enableTransparency,
            SystemParameters.HighContrast,
            themeStyle,
            startColorMenu,
            colorPrevalence,
            accentPalette,
            accentColorMenu);
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

    private static MediaColor ReadNavigationOverlayColor()
    {
        object? colorPrevalence = null;
        object? accentColorMenu = null;
        byte[]? accentPalette = null;
        try
        {
            using var personalize = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            colorPrevalence = personalize?.GetValue("ColorPrevalence");
            using var accent = Registry.CurrentUser.OpenSubKey(AccentRegistryPath);
            accentColorMenu = accent?.GetValue("AccentColorMenu");
            accentPalette = accent?.GetValue("AccentPalette") as byte[];
        }
        catch (Exception)
        {
        }

        return ResolveWin10NavigationOverlayColor(
            colorPrevalence,
            accentColorMenu,
            accentPalette,
            MediaColor.FromArgb(Win10NavigationOverlayAlpha, 0x2C, 0x2C, 0x2C));
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

    private static int PackAccentPolicyColor(MediaColor color, uint alpha) =>
        unchecked((int)((alpha << 24) | ((uint)color.B << 16) | ((uint)color.G << 8) | color.R));

    internal static MediaColor Blend(MediaColor source, MediaColor target, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return MediaColor.FromRgb(
            (byte)Math.Round(source.R + ((target.R - source.R) * amount)),
            (byte)Math.Round(source.G + ((target.G - source.G) * amount)),
            (byte)Math.Round(source.B + ((target.B - source.B) * amount)));
    }

    internal static MediaColor ScaleSaturation(MediaColor color, double factor)
    {
        factor = Math.Max(0, factor);
        var maximum = Math.Max(color.R, Math.Max(color.G, color.B));

        static byte ScaleChannel(byte channel, byte maximum, double factor) =>
            (byte)Math.Clamp(Math.Round(maximum - ((maximum - channel) * factor)), 0, 255);

        return MediaColor.FromArgb(
            color.A,
            ScaleChannel(color.R, maximum, factor),
            ScaleChannel(color.G, maximum, factor),
            ScaleChannel(color.B, maximum, factor));
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