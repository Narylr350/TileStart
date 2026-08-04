using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TileStart.Host.Tiles.Folders;

namespace TileStart.Host.Windowing;

internal static class ExpanderMotion
{
    private const string ExpandSiteName = "ExpandSite";
    private const double CollapsedOffset = -6;
    private static readonly ConditionalWeakTable<FrameworkElement, MotionState> States = new();

    private sealed class MotionState
    {
        public int Generation { get; set; }
    }

    public static void Synchronize(Expander expander)
    {
        if (!TryResolveSite(expander, out var site, out var translation))
        {
            return;
        }

        States.GetValue(site, static _ => new MotionState()).Generation++;
        site.BeginAnimation(FrameworkElement.HeightProperty, null);
        site.BeginAnimation(UIElement.OpacityProperty, null);
        translation.BeginAnimation(TranslateTransform.YProperty, null);
        site.Height = expander.IsExpanded ? double.NaN : 0;
        site.Opacity = expander.IsExpanded ? 1 : 0;
        translation.Y = expander.IsExpanded ? 0 : CollapsedOffset;
    }

    public static void Animate(Expander expander, bool expanding)
    {
        if (!expander.IsLoaded || !SystemParameters.ClientAreaAnimation)
        {
            Synchronize(expander);
            return;
        }

        if (!TryResolveSite(expander, out var site, out var translation))
        {
            return;
        }

        var state = States.GetValue(site, static _ => new MotionState());
        var generation = ++state.Generation;
        var currentHeight = Math.Max(0, site.ActualHeight);
        var currentOpacity = site.Opacity;
        var currentOffset = translation.Y;

        site.BeginAnimation(FrameworkElement.HeightProperty, null);
        site.BeginAnimation(UIElement.OpacityProperty, null);
        translation.BeginAnimation(TranslateTransform.YProperty, null);

        var targetHeight = expanding ? MeasureExpandedHeight(site, expander.ActualWidth) : 0;
        site.Height = currentHeight;
        site.Opacity = currentOpacity;
        translation.Y = currentOffset;

        var duration = expanding
            ? Win10FolderMotion.TilePreviewEnterDurationMilliseconds
            : Win10FolderMotion.TilePreviewExitDurationMilliseconds;
        var heightAnimation = Win10FolderMotion.CreateSplineAnimation(
            currentHeight,
            targetHeight,
            0,
            duration,
            Win10FolderMotion.StandardSpline,
            FillBehavior.HoldEnd);
        heightAnimation.Completed += (_, _) =>
        {
            if (state.Generation != generation || expander.IsExpanded != expanding)
            {
                return;
            }

            site.BeginAnimation(FrameworkElement.HeightProperty, null);
            site.BeginAnimation(UIElement.OpacityProperty, null);
            translation.BeginAnimation(TranslateTransform.YProperty, null);
            site.Height = expanding ? double.NaN : 0;
            site.Opacity = expanding ? 1 : 0;
            translation.Y = expanding ? 0 : CollapsedOffset;
        };

        site.BeginAnimation(
            FrameworkElement.HeightProperty,
            heightAnimation,
            HandoffBehavior.SnapshotAndReplace);
        site.BeginAnimation(
            UIElement.OpacityProperty,
            Win10FolderMotion.CreateSplineAnimation(
                currentOpacity,
                expanding ? 1 : 0,
                0,
                duration,
                Win10FolderMotion.StandardSpline,
                FillBehavior.HoldEnd),
            HandoffBehavior.SnapshotAndReplace);
        translation.BeginAnimation(
            TranslateTransform.YProperty,
            Win10FolderMotion.CreateSplineAnimation(
                currentOffset,
                expanding ? 0 : CollapsedOffset,
                0,
                duration,
                Win10FolderMotion.StandardSpline,
                FillBehavior.HoldEnd),
            HandoffBehavior.SnapshotAndReplace);
    }

    private static double MeasureExpandedHeight(FrameworkElement site, double availableWidth)
    {
        site.Height = double.NaN;
        site.Measure(new System.Windows.Size(
            availableWidth > 0 ? availableWidth : double.PositiveInfinity,
            double.PositiveInfinity));
        return Math.Max(0, site.DesiredSize.Height);
    }

    private static bool TryResolveSite(
        Expander expander,
        out FrameworkElement site,
        out TranslateTransform translation)
    {
        expander.ApplyTemplate();
        if (expander.Template.FindName(ExpandSiteName, expander) is not FrameworkElement resolvedSite)
        {
            site = null!;
            translation = null!;
            return false;
        }

        site = resolvedSite;
        if (site.RenderTransform is TranslateTransform existing)
        {
            // ControlTemplate 中的 Freezable 可能被 WPF 冻结并跨实例共享；动画前必须克隆，
            // 否则首次打开含 Expander 的设置窗口会在 BeginAnimation 时直接终止 Host。
            translation = existing.IsFrozen ? existing.CloneCurrentValue() : existing;
            if (!ReferenceEquals(translation, existing))
            {
                site.RenderTransform = translation;
            }

            return true;
        }

        translation = new TranslateTransform();
        site.RenderTransform = translation;
        return true;
    }
}
