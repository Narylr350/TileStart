using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using Canvas = System.Windows.Controls.Canvas;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using ItemsControl = System.Windows.Controls.ItemsControl;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using TileStart.Host.Applications;
using TileStart.Host.Icons;
using TileStart.Host.Shell;
using TileStart.Host.Windowing;
using TileStart.Host.Navigation;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Tiles.Layout;
using TileStart.Host.Tiles.DragDrop;
using TileStart.Host.Tiles.Settings;
using TileStart.Host.Persistence;
using TileStart.Host.Utilities;
using TileStart.Host.Tiles.Folders;

namespace TileStart.Host;

public partial class MainWindow
{
    private int _appFolderAnimationGeneration;
    private int _tileFolderAnimationGeneration;
    private bool _isAppFolderAnimating;
    private bool _isTileFolderAnimating;

    private async Task ToggleAppFolderAsync(AppEntry folder)
    {
        if (_isAppFolderAnimating)
        {
            return;
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            folder.IsExpanded = !folder.IsExpanded;
            return;
        }

        _isAppFolderAnimating = true;
        var generation = ++_appFolderAnimationGeneration;
        try
        {
            if (!folder.IsExpanded)
            {
                var expandPreviousPositions = CaptureAppEntryPositions();
                folder.IsExpanded = true;
                UpdateLayout();
                AnimateAppEntryReflowFrom(expandPreviousPositions);
                AnimateAppFolderChildren(folder, expanding: true);
                await Task.Delay(Win10FolderMotion.AppOpenDuration(folder.Children.Count));
                return;
            }

            var collapseControl = FindAppFolderControl(folder);
            if (collapseControl is not null)
            {
                collapseControl.BeginAnimation(
                    FrameworkElement.HeightProperty,
                    Win10FolderMotion.CreateSplineAnimation(
                        collapseControl.ActualHeight,
                        0,
                        0,
                        Win10FolderMotion.AppChildDurationMilliseconds,
                        Win10FolderMotion.StandardSpline,
                        FillBehavior.HoldEnd),
                    HandoffBehavior.SnapshotAndReplace);
            }

            AnimateAppFolderChildren(folder, expanding: false);
            await Task.Delay(Win10FolderMotion.AppChildDurationMilliseconds);
            if (generation != _appFolderAnimationGeneration)
            {
                return;
            }

            folder.IsExpanded = false;
            collapseControl?.BeginAnimation(FrameworkElement.HeightProperty, null);
            UpdateLayout();
        }
        finally
        {
            if (generation == _appFolderAnimationGeneration)
            {
                _isAppFolderAnimating = false;
            }
        }
    }

    private Dictionary<AppEntry, System.Windows.Point> CaptureAppEntryPositions()
    {
        var positions = new Dictionary<AppEntry, System.Windows.Point>();
        foreach (var button in FindVisualDescendants<Button>(AppsList))
        {
            if (button.Tag is not AppEntry app
                || button.Parent is not FrameworkElement root
                || !root.IsVisible
                || !root.IsDescendantOf(AppsList))
            {
                continue;
            }

            positions[app] = root.TransformToAncestor(AppsList).Transform(new System.Windows.Point());
        }

        return positions;
    }

    private void AnimateAppEntryReflowFrom(IReadOnlyDictionary<AppEntry, System.Windows.Point> previousPositions)
    {
        foreach (var (app, previous) in previousPositions)
        {
            var root = FindAppEntryRoot(app);
            if (root is null)
            {
                continue;
            }

            var current = root.TransformToAncestor(AppsList).Transform(new System.Windows.Point());
            var delta = previous.Y - current.Y;
            AnimateTranslateY(
                root,
                delta,
                Win10FolderMotion.AppReflowDurationMilliseconds,
                Win10FolderMotion.StandardSpline);
        }
    }

    private FrameworkElement? FindAppEntryRoot(AppEntry app) =>
        FindVisualDescendants<Button>(AppsList)
            .FirstOrDefault(button => ReferenceEquals(button.Tag, app))?.Parent as FrameworkElement;

    private ItemsControl? FindAppFolderControl(AppEntry folder) =>
        FindVisualDescendants<ItemsControl>(AppsList)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Tag, folder));

    private void AnimateAppFolderChildren(AppEntry folder, bool expanding)
    {
        var control = FindAppFolderControl(folder);
        if (control is null)
        {
            return;
        }

        control.UpdateLayout();
        for (var index = 0; index < folder.Children.Count; index++)
        {
            if (control.ItemContainerGenerator.ContainerFromItem(folder.Children[index]) is not FrameworkElement child)
            {
                continue;
            }

            var delay = expanding ? Win10FolderMotion.AppChildDelay(index) : 0;
            var from = expanding ? -Win10VisualMetrics.AllAppsRowHeight : 0;
            var to = expanding ? 0 : -Win10VisualMetrics.AllAppsRowHeight;
            var transform = new TranslateTransform();
            child.RenderTransform = transform;
            child.Opacity = 1;
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                Win10FolderMotion.CreateSplineAnimation(
                    from,
                    to,
                    delay,
                    Win10FolderMotion.AppChildDurationMilliseconds,
                    Win10FolderMotion.StandardSpline,
                    expanding ? FillBehavior.Stop : FillBehavior.HoldEnd),
                HandoffBehavior.SnapshotAndReplace);
            child.BeginAnimation(
                OpacityProperty,
                Win10FolderMotion.CreateSplineAnimation(
                    expanding ? 0 : 1,
                    expanding ? 1 : 0,
                    delay,
                    Win10FolderMotion.AppChildDurationMilliseconds,
                    Win10FolderMotion.StandardSpline,
                    expanding ? FillBehavior.Stop : FillBehavior.HoldEnd),
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private async Task ToggleTileFolderAsync(TileGroup group, TileItem folder)
    {
        if (_isTileFolderAnimating)
        {
            return;
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            folder.IsFolderExpanded = !folder.IsFolderExpanded;
            group.RefreshLayout();
            UpdateLayout();
            return;
        }

        _isTileFolderAnimating = true;
        var generation = ++_tileFolderAnimationGeneration;
        try
        {
            if (!folder.IsFolderExpanded)
            {
                var expandPreviousTops = group.Tiles.ToDictionary(item => item, item => item.DisplayTop);
                var expandPreviousGroupPositions = CaptureGroupReorderPositions();
                folder.IsFolderExpanded = true;
                group.RefreshLayout();
                UpdateLayout();
                var shiftDuration = AnimateTileFolderShift(group, expandPreviousTops, expanding: true);
                var expandMovedGroups = AnimateGroupReorderFrom(expandPreviousGroupPositions);
                AnimateTileFolderRegion(folder, expanding: true);
                await Task.Delay(Math.Max(
                    Math.Max(shiftDuration, Win10FolderMotion.TileRegionExpandDurationMilliseconds),
                    expandMovedGroups.Count == 0 ? 0 : Win10ReorderMotion.DurationMilliseconds));
                return;
            }

            var collapsePreviousTops = group.Tiles.ToDictionary(item => item, item => item.DisplayTop);
            var collapsePreviousGroupPositions = CaptureGroupReorderPositions();
            var collapseRegion = FindTileFolderRegion(folder);
            var collapseRegionContainer = collapseRegion?.Parent as FrameworkElement;
            if (collapseRegion is not null)
            {
                collapseRegion.Visibility = Visibility.Visible;
                collapseRegion.BeginAnimation(
                    FrameworkElement.HeightProperty,
                    Win10FolderMotion.CreateSplineAnimation(
                        collapseRegion.ActualHeight,
                        collapseRegion.ActualHeight,
                        0,
                        Win10FolderMotion.TileRegionCollapseDurationMilliseconds,
                        Win10FolderMotion.StandardSpline,
                        FillBehavior.HoldEnd),
                    HandoffBehavior.SnapshotAndReplace);
                collapseRegionContainer?.BeginAnimation(
                    Canvas.TopProperty,
                    Win10FolderMotion.CreateSplineAnimation(
                        folder.FolderRegionTop,
                        folder.FolderRegionTop,
                        0,
                        Win10FolderMotion.TileRegionCollapseDurationMilliseconds,
                        Win10FolderMotion.StandardSpline,
                        FillBehavior.HoldEnd),
                    HandoffBehavior.SnapshotAndReplace);
            }

            folder.IsFolderExpanded = false;
            group.RefreshLayout();
            UpdateLayout();
            var collapseDuration = AnimateTileFolderShift(group, collapsePreviousTops, expanding: false);
            var collapseMovedGroups = AnimateGroupReorderFrom(collapsePreviousGroupPositions);
            AnimateTileFolderRegion(folder, expanding: false);
            var totalCollapseDuration = Math.Max(
                collapseDuration,
                Math.Max(
                    Win10FolderMotion.TileRegionCollapseDurationMilliseconds,
                    collapseMovedGroups.Count == 0 ? 0 : Win10ReorderMotion.DurationMilliseconds));
            await Task.Delay(Win10FolderMotion.TileRegionCollapseDurationMilliseconds);
            if (generation != _tileFolderAnimationGeneration)
            {
                return;
            }

            if (collapseRegion is not null)
            {
                collapseRegion.BeginAnimation(FrameworkElement.HeightProperty, null);
                collapseRegionContainer?.BeginAnimation(Canvas.TopProperty, null);
                collapseRegion.ClearValue(VisibilityProperty);
            }

            await Task.Delay(Math.Max(
                0,
                totalCollapseDuration - Win10FolderMotion.TileRegionCollapseDurationMilliseconds));
        }
        finally
        {
            if (generation == _tileFolderAnimationGeneration)
            {
                _isTileFolderAnimating = false;
            }
        }
    }

    private int AnimateTileFolderShift(
        TileGroup group,
        IReadOnlyDictionary<TileItem, double> previousTops,
        bool expanding)
    {
        var groupControl = FindVisualDescendants<ItemsControl>(TileGroupsControl)
            .FirstOrDefault(control => ReferenceEquals(control.Tag, group));
        if (groupControl is null)
        {
            return 0;
        }

        var rowCount = Math.Max(
            2,
            group.Tiles.Count == 0
                ? 2
                : group.Tiles.Max(tile => tile.Row + tile.Size.RowSpan()));
        var maximumDuration = 0;
        foreach (var tile in group.Tiles)
        {
            if (!previousTops.TryGetValue(tile, out var previousTop)
                || groupControl.ItemContainerGenerator.ContainerFromItem(tile) is not FrameworkElement container)
            {
                continue;
            }

            var delta = previousTop - tile.DisplayTop;
            var duration = Win10FolderMotion.TileShiftDuration(
                expanding,
                tile.Row,
                tile.Column,
                rowCount,
                Win10TileMetrics.GroupColumns);
            if (AnimateTranslateY(
                    container,
                    delta,
                    duration,
                    expanding
                        ? Win10FolderMotion.TileExpandShiftSpline
                        : Win10FolderMotion.StandardSpline))
            {
                maximumDuration = Math.Max(maximumDuration, duration);
            }
        }

        return maximumDuration;
    }

    private void AnimateTileFolderRegion(TileItem folder, bool expanding)
    {
        var region = FindTileFolderRegion(folder);
        if (region is null)
        {
            return;
        }

        var scale = new ScaleTransform(1, 1);
        region.RenderTransformOrigin = new System.Windows.Point(0.5, 0);
        region.RenderTransform = scale;
        region.Opacity = 1;
        var duration = expanding
            ? Win10FolderMotion.TileRegionExpandDurationMilliseconds
            : Win10FolderMotion.TileRegionCollapseDurationMilliseconds;
        var fillBehavior = expanding ? FillBehavior.Stop : FillBehavior.HoldEnd;
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            Win10FolderMotion.CreateSplineAnimation(
                expanding ? 0 : 1,
                expanding ? 1 : 0,
                Win10FolderMotion.TileRegionExpandDelayMilliseconds,
                duration,
                Win10FolderMotion.StandardSpline,
                fillBehavior),
            HandoffBehavior.SnapshotAndReplace);
        region.BeginAnimation(
            OpacityProperty,
            Win10FolderMotion.CreateSplineAnimation(
                expanding ? 0 : 1,
                expanding ? 1 : 0,
                Win10FolderMotion.TileRegionExpandDelayMilliseconds,
                duration,
                Win10FolderMotion.StandardSpline,
                fillBehavior),
            HandoffBehavior.SnapshotAndReplace);
    }

    private System.Windows.Controls.Border? FindTileFolderRegion(TileItem folder) =>
        FindVisualDescendants<System.Windows.Controls.Border>(TileGroupsControl)
            .FirstOrDefault(border => border.Name == "FolderRegion" && ReferenceEquals(border.DataContext, folder));

    private static bool AnimateTranslateY(
        FrameworkElement element,
        double delta,
        int durationMilliseconds,
        KeySpline spline)
    {
        if (Math.Abs(delta) < 0.1)
        {
            return false;
        }

        var transform = new TranslateTransform(0, 0);
        element.RenderTransform = transform;
        transform.BeginAnimation(
            TranslateTransform.YProperty,
            Win10FolderMotion.CreateSplineAnimation(delta, 0, 0, durationMilliseconds, spline),
            HandoffBehavior.SnapshotAndReplace);
        return true;
    }

    private bool FindTileLocation(TileItem tile, out TileGroup group, out TileItem? folder)
    {
        foreach (var candidate in TileLayout.Groups)
        {
            if (candidate.Tiles.Contains(tile))
            {
                group = candidate;
                folder = null;
                return true;
            }

            var parentFolder = candidate.Tiles.FirstOrDefault(item =>
                item.IsTileFolder && item.FolderTiles.Contains(tile));
            if (parentFolder is not null)
            {
                group = candidate;
                folder = parentFolder;
                return true;
            }
        }

        group = null!;
        folder = null;
        return false;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
