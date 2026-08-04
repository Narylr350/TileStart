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
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmSystemBackdropType = 38;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCaptionColor = 35;
    private const int DwmSystemBackdropNone = 0;
    private const int DwmSystemBackdropTransientWindow = 3;
    private const int DwmWindowCornerRound = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    public static void Apply(Window window)
    {
        if (AppThemeManager.CurrentStyle != AppThemeStyle.Windows11
            || window.TryFindResource("TileStartWindowBackgroundBrush") is not SolidColorBrush fallbackBrush)
        {
            return;
        }

        var useDarkMode = IsDarkSurface(fallbackBrush.Color);
        var useMaterial = Win10Theme.ReadStartMaterial(AppThemeStyle.Windows11, useDarkMode).UseAcrylic;
        var backdropType = ResolveBackdropType(AppThemeManager.CurrentStyle, useMaterial);
        if (backdropType == DwmSystemBackdropNone)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        var darkMode = useDarkMode ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref darkMode, sizeof(int));
        var cornerPreference = DwmWindowCornerRound;
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref cornerPreference, sizeof(int));
        var captionColor = ToColorRef(fallbackBrush.Color);
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

        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        // 系统 backdrop 由 DWM 绘制在窗口后方；WPF 根表面必须透明，否则只会看到原来的实体色。
        // 仅在 DWM 明确返回成功后清空背景，旧系统和透明效果关闭时仍保留 XAML 回退色。
        window.Background = MediaBrushes.Transparent;
        if (window.Content is Border surface
            && window.TryFindResource("TileStartDialogBackdropBrush") is System.Windows.Media.Brush backdropBrush)
        {
            surface.Background = backdropBrush;
        }

    }

    internal static int ResolveBackdropType(AppThemeStyle style, bool useMaterial) =>
        style == AppThemeStyle.Windows11 && useMaterial
            // 设置和编辑窗口是覆盖在开始菜单上的临时表面。MainWindow Mica 只采样壁纸，
            // 深色模式下近似纯黑，无法形成用户可见的背景模糊；TransientWindow 才是
            // Windows 11 为此类短期窗口提供的系统 Acrylic backdrop。
            ? DwmSystemBackdropTransientWindow
            : DwmSystemBackdropNone;

    internal static bool IsDarkSurface(MediaColor color) =>
        (color.R * 299 + color.G * 587 + color.B * 114) < 128_000;

    internal static int ToColorRef(MediaColor color) => color.R | color.G << 8 | color.B << 16;

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);
}
