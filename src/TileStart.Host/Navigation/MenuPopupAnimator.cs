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

        PopupMaterialManager.Clear(border);
        var opensUpward = ContextMenuOpensUpward(menu, border);
        if (!Animate(
                border,
                Win10MenuPopupMotion.TopLevelClosedRatio,
                opensUpward,
                opensUpward ? null : PointerOriginY(border),
                () => PopupMaterialManager.Apply(
                    border,
                    Win10MenuPopupMotion.MaterialTransitionDurationMilliseconds)))
        {
            PopupMaterialManager.Apply(border);
        }
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

        PopupMaterialManager.Clear(border);
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

        if (!Animate(
                border,
                Win10MenuPopupMotion.SubmenuClosedRatio,
                opensUpward,
                opensUpward ? null : PointerOriginY(border),
                () => PopupMaterialManager.Apply(
                    border,
                    Win10MenuPopupMotion.MaterialTransitionDurationMilliseconds)))
        {
            PopupMaterialManager.Apply(border);
        }
    }

    public static void CloseSubmenu(object? sender)
    {
        if (sender is Popup { Child: Border border })
        {
            border.ClearValue(UIElement.ClipProperty);
            PopupMaterialManager.Clear(border);
        }
    }

    private static bool Animate(
        Border border,
        double closedRatio,
        bool opensUpward,
        double? pointerOriginY,
        Action materialReady)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return false;
        }

        border.UpdateLayout();
        if (border.ActualWidth <= 0 || border.ActualHeight <= 0)
        {
            return false;
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

            materialReady();
        };
        clip.BeginAnimation(RectangleGeometry.RectProperty, animation, HandoffBehavior.SnapshotAndReplace);
        return true;
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
