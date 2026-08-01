using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TileStart.Host.Navigation;

internal static class MenuPopupAnimator
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    public static void OpenTopLevel(ContextMenu menu)
    {
        if (GetContextMenuPopupBorder(menu) is not { } border)
        {
            return;
        }

        PopupMaterialManager.Apply(border);
        var opensUpward = ContextMenuOpensUpward(menu, border);
        Animate(
            border,
            Win10MenuPopupMotion.TopLevelClosedRatio,
            opensUpward,
            opensUpward ? null : PointerOriginY(border));
    }

    public static void CloseTopLevel(ContextMenu menu)
    {
        if (GetContextMenuPopupBorder(menu) is { } border)
        {
            border.ClearValue(UIElement.ClipProperty);
            PopupMaterialManager.Clear(border);
        }
    }

    public static void OpenSubmenu(object? sender)
    {
        if (!SystemParameters.ClientAreaAnimation
            || sender is not Popup { Child: Border border } popup)
        {
            return;
        }

        border.UpdateLayout();
        if (border.ActualWidth <= 0 || border.ActualHeight <= 0)
        {
            return;
        }

        PopupMaterialManager.Apply(border);
        var opensUpward = false;
        if (popup.PlacementTarget is FrameworkElement placementTarget)
        {
            try
            {
                opensUpward = border.PointToScreen(new System.Windows.Point()).Y
                              < placementTarget.PointToScreen(new System.Windows.Point()).Y - 0.5;
            }
            catch (InvalidOperationException)
            {
            }
        }

        Animate(
            border,
            Win10MenuPopupMotion.SubmenuClosedRatio,
            opensUpward,
            opensUpward ? null : PointerOriginY(border));
    }

    public static void CloseSubmenu(object? sender)
    {
        if (sender is Popup { Child: Border border })
        {
            border.ClearValue(UIElement.ClipProperty);
            PopupMaterialManager.Clear(border);
        }
    }

    private static void Animate(
        Border border,
        double closedRatio,
        bool opensUpward,
        double? pointerOriginY)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        border.UpdateLayout();
        if (border.ActualWidth <= 0 || border.ActualHeight <= 0)
        {
            return;
        }

        var clip = new RectangleGeometry(new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight));
        border.Clip = clip;
        var animation = Win10MenuPopupMotion.CreateOpenAnimation(
            border.ActualWidth,
            border.ActualHeight,
            closedRatio,
            opensUpward,
            pointerOriginY,
            useSubmenuDirection: true);
        animation.Completed += (_, _) =>
        {
            if (ReferenceEquals(border.Clip, clip))
            {
                border.ClearValue(UIElement.ClipProperty);
            }
        };
        clip.BeginAnimation(RectangleGeometry.RectProperty, animation, HandoffBehavior.SnapshotAndReplace);
        if (border.Child is UIElement content)
        {
            // Acrylic 由 Popup HWND 立即呈现；内容使用同周期淡入补足可感知的展开过程。
            // 只动画子内容，不降低根窗口 alpha，避免重新引入原生点击穿透。
            content.BeginAnimation(
                UIElement.OpacityProperty,
                Win10MenuPopupMotion.CreateContentFadeAnimation(),
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private static Border? GetContextMenuPopupBorder(ContextMenu menu) =>
        menu.Template.FindName("ContextMenuPopupBorder", menu) as Border;

    private static bool ContextMenuOpensUpward(ContextMenu menu, Border border)
    {
        try
        {
            var menuTop = border.PointToScreen(new System.Windows.Point()).Y;
            if (menu.Placement == PlacementMode.Right && menu.PlacementTarget is FrameworkElement placementTarget)
            {
                return menuTop < placementTarget.PointToScreen(new System.Windows.Point()).Y - 0.5;
            }

            return GetCursorPos(out var cursor) && menuTop < cursor.Y - 0.5;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static double? PointerOriginY(FrameworkElement popup)
    {
        if (!GetCursorPos(out var cursor))
        {
            return null;
        }

        try
        {
            var localPointer = popup.PointFromScreen(new System.Windows.Point(cursor.X, cursor.Y));
            return localPointer.Y >= 0 && localPointer.Y <= popup.ActualHeight ? localPointer.Y : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
