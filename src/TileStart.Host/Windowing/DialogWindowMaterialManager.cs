using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using TileStart.Host.Themes;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace TileStart.Host.Windowing;

internal static class DialogWindowMaterialManager
{
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int AcrylicAccentFlags = 2;
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmSystemBackdropType = 38;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCaptionColor = 35;
    private const int DwmSystemBackdropNone = 0;
    private const int DwmSystemBackdropTransientWindow = 3;
    private const int DwmWindowCornerDoNotRound = 1;
    private const int DwmWindowCornerRound = 2;
    private const int DwmColorNone = unchecked((int)0xFFFFFFFE);
    private const byte Win10DialogTintAlpha = 0xCC;

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    public static void Apply(Window window)
    {
        if (window.TryFindResource("TileStartWindowBackgroundBrush") is not SolidColorBrush fallbackBrush)
        {
            return;
        }

        var style = AppThemeManager.CurrentStyle;
        var useDarkMode = IsDarkSurface(fallbackBrush.Color);
        var material = Win10Theme.ReadStartMaterial(style, useDarkMode);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        var darkMode = useDarkMode ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref darkMode, sizeof(int));
        var cornerPreference = ResolveCornerPreference(style);
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref cornerPreference, sizeof(int));

        if (style == AppThemeStyle.Windows10)
        {
            var accentState = ResolveLegacyAccentState(style, material.UseAcrylic);
            var tintBrush = window.TryFindResource("TileStartDialogBackdropBrush") as SolidColorBrush
                            ?? fallbackBrush;
            var gradientColor = ComposeGradientColor(tintBrush.Color, Win10DialogTintAlpha);
            if (accentState == AccentDisabled
                || !SetAccentPolicy(handle, accentState, AcrylicAccentFlags, gradientColor))
            {
                return;
            }

            var legacyCaptionColor = ResolveCaptionColor(materialApplied: true, fallbackBrush.Color);
            _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref legacyCaptionColor, sizeof(int));
            ApplyTransparentSurface(window, MediaBrushes.Transparent);
            return;
        }

        var backdropType = ResolveBackdropType(style, material.UseAcrylic);
        if (backdropType == DwmSystemBackdropNone)
        {
            return;
        }

        // 自绘标题栏必须让完整客户区的 Transient Acrylic 继续透出。
        // 使用实体 CaptionColor 会在 Acrylic 上方额外画出一条 #202020/#F3F3F3 色带。
        var captionColor = ResolveCaptionColor(materialApplied: true, fallbackBrush.Color);
        _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref captionColor, sizeof(int));
        var backdropResult = DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref backdropType, sizeof(int));
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        var frameResult = backdropResult == 0
            ? DwmExtendFrameIntoClientArea(handle, ref margins)
            : backdropResult;

        if (backdropResult != 0 || frameResult != 0)
        {
            return;
        }

        if (window.TryFindResource("TileStartDialogBackdropBrush") is System.Windows.Media.Brush backdropBrush)
        {
            ApplyTransparentSurface(window, backdropBrush);
        }
    }

    internal static int ResolveBackdropType(AppThemeStyle style, bool useMaterial) =>
        style == AppThemeStyle.Windows11 && useMaterial
            // 设置和编辑窗口是覆盖在开始菜单上的临时表面。MainWindow Mica 只采样壁纸，
            // 深色模式下近似纯黑，无法形成用户可见的背景模糊；TransientWindow 才是
            // Windows 11 为此类短期窗口提供的系统 Acrylic backdrop。
            ? DwmSystemBackdropTransientWindow
            : DwmSystemBackdropNone;

    internal static int ResolveLegacyAccentState(AppThemeStyle style, bool useMaterial) =>
        style == AppThemeStyle.Windows10 && useMaterial
            ? AccentEnableAcrylicBlurBehind
            : AccentDisabled;

    internal static int ResolveCornerPreference(AppThemeStyle style) =>
        style == AppThemeStyle.Windows11
            ? DwmWindowCornerRound
            : DwmWindowCornerDoNotRound;

    internal static bool IsDarkSurface(MediaColor color) =>
        (color.R * 299 + color.G * 587 + color.B * 114) < 128_000;

    internal static int ResolveCaptionColor(bool materialApplied, MediaColor fallbackColor) =>
        materialApplied ? DwmColorNone : ToColorRef(fallbackColor);

    internal static int ToColorRef(MediaColor color) => color.R | color.G << 8 | color.B << 16;

    internal static int ComposeGradientColor(MediaColor color, byte alpha) =>
        unchecked((int)(((uint)alpha << 24) | ((uint)color.B << 16) | ((uint)color.G << 8) | color.R));

    private static void ApplyTransparentSurface(Window window, System.Windows.Media.Brush surfaceBrush)
    {
        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        // WCA Acrylic 和 DWM backdrop 都绘制在窗口内容后方；仅在原生调用成功后
        // 清空 WPF 根背景，透明效果关闭或调用失败时继续使用 XAML 回退色。
        window.Background = MediaBrushes.Transparent;
        if (window.Content is Border surface)
        {
            surface.Background = surfaceBrush;
        }
    }

    private static bool SetAccentPolicy(nint window, int accentState, int accentFlags, int gradientColor)
    {
        var accent = new AccentPolicy
        {
            AccentState = accentState,
            AccentFlags = accentFlags,
            GradientColor = gradientColor,
        };
        var accentPointer = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try
        {
            Marshal.StructureToPtr(accent, accentPointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = accentPointer,
                SizeOfData = Marshal.SizeOf<AccentPolicy>(),
            };
            return SetWindowCompositionAttribute(window, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPointer);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        nint window,
        ref WindowCompositionAttributeData data);
}
