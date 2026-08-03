using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TileStart.Host.Themes;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Navigation;

internal static class PopupMaterialManager
{
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int AcrylicAccentFlags = 2;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerDoNotRound = 1;
    private const int DwmWindowCornerRound = 2;
    private const byte Win10TransientTintAlpha = 0xCC;
    private const byte Win11TransientTintAlpha = 0xD9;
    private const byte PopupHitTestAlpha = 1;
    private static readonly ConditionalWeakTable<System.Windows.Controls.Border, PopupState> States = new();

    private sealed class PopupState
    {
        public System.Windows.Media.Brush? Background { get; init; }
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

    public static void Apply(
        System.Windows.Controls.Border popupSurface,
        int transitionDurationMilliseconds = 0)
    {
        if (PresentationSource.FromVisual(popupSurface) is not HwndSource source)
        {
            return;
        }

        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        var themeStyle = AppThemeManager.CurrentStyle;
        ApplyCornerPreference(source.Handle, themeStyle);
        var useAcrylic = Win10Theme.ReadStartMaterial(themeStyle).UseAcrylic;
        if (!useAcrylic || popupSurface.Background is not SolidColorBrush fallbackBrush)
        {
            SetAccentPolicy(source.Handle, AccentDisabled, 0, 0);
            RestoreBackground(popupSurface);
            return;
        }

        var tintBrush = popupSurface.TryFindResource("TileStartPopupAcrylicTintBrush") as SolidColorBrush
                        ?? fallbackBrush;
        // Popup 是独立的分层 HWND，必须在它自己的窗口上启用 HostBackdrop Acrylic。
        // Win10 临时浮层使用 0.8 tint；Win11 使用已导出 Acrylic 资源的 0.85 tint。
        // WCA 没有等价的独立 TintOpacity，只能映射到 GradientColor alpha。
        if (SetAccentPolicy(
                source.Handle,
                AccentEnableAcrylicBlurBehind,
                AcrylicAccentFlags,
                ComposeGradientColor(tintBrush.Color, ResolveTransientTintAlpha(themeStyle))))
        {
            if (!States.TryGetValue(popupSurface, out _))
            {
                States.Add(popupSurface, new PopupState { Background = popupSurface.Background });
            }

            if (transitionDurationMilliseconds > 0)
            {
                var transitionBrush = new SolidColorBrush(fallbackBrush.Color);
                popupSurface.Background = transitionBrush;
                var transition = CreateMaterialTransitionAnimation(
                    fallbackBrush.Color,
                    transitionDurationMilliseconds);
                transition.Completed += (_, _) =>
                {
                    if (ReferenceEquals(popupSurface.Background, transitionBrush))
                    {
                        popupSurface.Background = CreateHitTestBackground(fallbackBrush.Color);
                    }
                };
                transitionBrush.BeginAnimation(
                    SolidColorBrush.ColorProperty,
                    transition,
                    HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                // AllowsTransparency Popup 使用分层 HWND。若 WPF 内容完全透明，WCA 虽能画出
                // Acrylic，原生窗口仍可能把 alpha=0 像素视为透明并把鼠标交给下方磁贴。
                // 保留最小非零 alpha 仅用于维持 HWND 命中区域，不承担可见 tint。
                popupSurface.Background = CreateHitTestBackground(fallbackBrush.Color);
            }
        }
    }

    public static void Clear(System.Windows.Controls.Border popupSurface)
    {
        if (PresentationSource.FromVisual(popupSurface) is HwndSource source)
        {
            SetAccentPolicy(source.Handle, AccentDisabled, 0, 0);
        }

        RestoreBackground(popupSurface);
    }

    internal static int ComposeGradientColor(System.Windows.Media.Color color, byte alpha) =>
        unchecked((int)(((uint)alpha << 24) | ((uint)color.B << 16) | ((uint)color.G << 8) | color.R));

    internal static byte ResolveTransientTintAlpha(AppThemeStyle themeStyle) =>
        themeStyle == AppThemeStyle.Windows11 ? Win11TransientTintAlpha : Win10TransientTintAlpha;

    internal static int ResolveCornerPreference(AppThemeStyle themeStyle) =>
        themeStyle == AppThemeStyle.Windows11 ? DwmWindowCornerRound : DwmWindowCornerDoNotRound;

    internal static SolidColorBrush CreateHitTestBackground(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
            PopupHitTestAlpha,
            color.R,
            color.G,
            color.B));
        brush.Freeze();
        return brush;
    }

    internal static ColorAnimation CreateMaterialTransitionAnimation(
        System.Windows.Media.Color color,
        int durationMilliseconds) =>
        new()
        {
            To = System.Windows.Media.Color.FromArgb(PopupHitTestAlpha, color.R, color.G, color.B),
            Duration = TimeSpan.FromMilliseconds(Math.Max(0, durationMilliseconds)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd,
        };

    private static void RestoreBackground(System.Windows.Controls.Border popupSurface)
    {
        if (States.TryGetValue(popupSurface, out var state))
        {
            popupSurface.Background = state.Background;
            States.Remove(popupSurface);
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

    private static void ApplyCornerPreference(nint window, AppThemeStyle themeStyle)
    {
        var preference = ResolveCornerPreference(themeStyle);
        _ = DwmSetWindowAttribute(
            window,
            DwmWindowCornerPreference,
            ref preference,
            Marshal.SizeOf<int>());
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        nint window,
        ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);
}
