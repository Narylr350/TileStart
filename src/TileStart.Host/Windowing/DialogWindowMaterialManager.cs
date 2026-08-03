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
    private const int DwmSystemBackdropNone = 0;
    private const int DwmSystemBackdropMainWindow = 2;
    private const int DwmWindowCornerRound = 2;

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
        if (DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref backdropType, sizeof(int)) != 0)
        {
            return;
        }

        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        // Mica 由 DWM 绘制在窗口后方；WPF 根表面必须透明，否则只会看到原来的实体色。
        // 仅在 DWM 明确返回成功后清空背景，旧系统和透明效果关闭时仍保留 XAML 回退色。
        window.Background = MediaBrushes.Transparent;
        if (window.Content is Border surface)
        {
            surface.Background = MediaBrushes.Transparent;
        }
    }

    internal static int ResolveBackdropType(AppThemeStyle style, bool useMaterial) =>
        style == AppThemeStyle.Windows11 && useMaterial
            ? DwmSystemBackdropMainWindow
            : DwmSystemBackdropNone;

    internal static bool IsDarkSurface(MediaColor color) =>
        (color.R * 299 + color.G * 587 + color.B * 114) < 128_000;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);
}
