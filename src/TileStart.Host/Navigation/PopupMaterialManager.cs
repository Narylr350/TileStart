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
    private const byte NativeTransientTintAlpha = 0xCC;
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
        var useAcrylic = AppThemeManager.CurrentStyle == AppThemeStyle.Windows10
                         && Win10Theme.ReadStartMaterial(AppThemeStyle.Windows10).UseAcrylic;
        if (!useAcrylic || popupSurface.Background is not SolidColorBrush fallbackBrush)
        {
            SetAccentPolicy(source.Handle, AccentDisabled, 0, 0);
            RestoreBackground(popupSurface);
            return;
        }

        // Win10 临时浮层使用 HostBackdrop 和 0.8 TintOpacity。WCA 没有等价的独立 tint
        // 参数，因此这里只能映射到 GradientColor alpha；调用失败必须保留已验证的回退色，
        // 不能留下完全透明的菜单。
        if (SetAccentPolicy(
                source.Handle,
                AccentEnableAcrylicBlurBehind,
                AcrylicAccentFlags,
                ComposeGradientColor(fallbackBrush.Color, NativeTransientTintAlpha)))
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

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        nint window,
        ref WindowCompositionAttributeData data);
}
