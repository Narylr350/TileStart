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
    private System.Windows.Point _dragStart;
    private System.Windows.Point _appDragStart;
    private System.Windows.Point _appDragAnchor;
    private readonly System.Windows.Threading.DispatcherTimer _tileReflowTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(TileDropResolver.ReflowDelayMilliseconds),
    };

    private readonly System.Windows.Threading.DispatcherTimer _folderActivationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(TileDropResolver.FolderActivationDelayMilliseconds),
    };
    private readonly TileReflowStability _folderActivationStability = new(TileDropResolver.FolderActivationDrift);
    private bool _tileDragAutoScrollSubscribed;
    private double _tileDragAutoScrollVelocity;
    private TimeSpan? _tileDragAutoScrollLastFrame;
    private System.Windows.Point _lastInternalTileDragPosition;
    private System.Windows.Point _dragAnchor;
    private TileGroup? _pendingDropTarget;
    private TileItem? _pendingDropFolder;
    private System.Windows.Point _pendingDropPosition;
    private TileItem? _pendingDropTile;
    private TileGroup? _pendingFolderDropGroup;
    private TileItem? _pendingFolderDropTarget;
    private TileGroup? _armedFolderDropGroup;
    private TileItem? _armedFolderDropTarget;
    private TileItem? _dragTile;
    private AppEntry? _appDragEntry;
    private Button? _appDragSourceElement;
    private TileGroup? _dragSource;
    private TileItem? _dragSourceFolder;
    private Button? _dragSourceElement;
    private TileDragTransaction? _dragTransaction;
    private TileDragHitGeometry? _tileDragHitGeometry;
    private bool _dragCompleted;
    private bool _isInternalTileDrag;
    private bool _isCompletingInternalTileDrag;
    private bool _isInternalAppDrag;
    private bool _internalDropIsValid;
    private System.Windows.Point _groupDragStart;
    private System.Windows.Vector _groupDragAnchor;
    private TileGroup? _groupDragGroup;
    private TileGroupHeader? _groupDragHeader;
    private TileGroupDragTransaction? _groupDragTransaction;
    private FrameworkElement? _groupDragContainer;
    private TranslateTransform? _groupDragTransform;
    private TileGroupDropTarget[] _groupDragTargets = [];
    private TileGroupCell? _groupDragTargetCell;
    private bool _isInternalGroupDrag;
    private bool _isCompletingGroupDrag;
#if DEBUG
    private string? _tileDropTraceCandidateKey;
    private System.Windows.Point _tileDropTraceCandidatePosition;
    private string? _tileDropGeometryTraceSignature;
#endif

    private void AppButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCompletingInternalTileDrag)
        {
            e.Handled = true;
            return;
        }

        _appDragSourceElement = sender as Button;
        _appDragEntry = _appDragSourceElement?.Tag as AppEntry;
        if (_appDragEntry?.IsFolder == true)
        {
            _appDragEntry = null;
            _appDragSourceElement = null;
            return;
        }

        _appDragStart = e.GetPosition(this);
        _appDragAnchor = _appDragSourceElement is null
            ? new System.Windows.Point()
            : e.GetPosition(_appDragSourceElement);
    }

    private void AppButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isInternalAppDrag
            || e.LeftButton != MouseButtonState.Pressed
            || _appDragEntry is null
            || _appDragSourceElement is null
            || _isInternalTileDrag)
        {
            return;
        }

        var delta = e.GetPosition(this) - _appDragStart;
        if (delta.LengthSquared <= 9)
        {
            return;
        }

        ShowInternalDragPreview(
            CaptureElement(_appDragSourceElement),
            _appDragSourceElement.ActualWidth,
            _appDragSourceElement.ActualHeight);
        _isInternalAppDrag = true;
        MoveAppDragPreview(e.GetPosition(MainSurface));
        Mouse.Capture(this, CaptureMode.SubTree);
        e.Handled = true;
    }

    private void MoveAppDragPreview(System.Windows.Point position)
    {
        MoveInternalDragPreview(position, _appDragAnchor);
    }

    private void EndInternalAppDrag(bool commit, System.Windows.Point position)
    {
        if (commit && _appDragEntry is { IsFolder: false } app)
        {
            var groupsPosition = MainSurface.TranslatePoint(position, TileGroupsControl);
            if (TryResolveTileAreaGroup(groupsPosition, out var target, out var groupControl))
            {
                AddAppTile(target, app, TileGroupsControl.TranslatePoint(groupsPosition, groupControl));
            }
            else
            {
                var panePosition = MainSurface.TranslatePoint(position, TilePane);
                if (panePosition.X >= 0
                    && panePosition.Y >= 0
                    && panePosition.X < TilePane.ActualWidth
                    && panePosition.Y < TilePane.ActualHeight)
                {
                    var group = TileGroupManager.Add(TileLayout);
                    AddAppTile(group, app, new System.Windows.Point());
                }
            }
        }

        Mouse.Capture(null);
        HideInternalDragPreview();
        _isInternalAppDrag = false;
        _appDragEntry = null;
        _appDragSourceElement = null;
        _suppressTileActivationUntil = Environment.TickCount64 + 300;
    }

    private void OpenAppFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AppEntry app })
        {
            if (IsMissingFileApplication(app))
            {
                RemoveApplicationFromList(LaunchTargetIdentity.GetKey(app.LaunchTarget));
                DiagnosticLog.Write($"Removed missing application entry: {app.LaunchTarget}");
                return;
            }

            AppLauncher.OpenFileLocation(app);
        }
    }

    private void GroupHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not TileGroupHeader { DataContext: TileGroup group } header
            || header.IsEditing
            || _isInternalTileDrag
            || _isInternalAppDrag)
        {
            return;
        }

        _groupDragGroup = group;
        _groupDragHeader = header;
        _groupDragStart = e.GetPosition(TileGroupsControl);
        header.CaptureMouse();
        e.Handled = true;
    }

    private void GroupHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!ReferenceEquals(sender, _groupDragHeader) || _groupDragGroup is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelPendingGroupDrag();
            return;
        }

        var position = e.GetPosition(TileGroupsControl);
        if (_groupDragTransaction is null)
        {
            var distance = position - _groupDragStart;
            if (Math.Abs(distance.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(distance.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (!BeginGroupDrag())
            {
                return;
            }
        }

        UpdateGroupDrag(position);
        e.Handled = true;
    }

    private void GroupHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !ReferenceEquals(sender, _groupDragHeader))
        {
            return;
        }

        if (_groupDragTransaction is null)
        {
            var header = _groupDragHeader;
            _isCompletingGroupDrag = true;
            ClearGroupDragState();
            header?.ReleaseMouseCapture();
            _isCompletingGroupDrag = false;
            header?.BeginEdit();
        }
        else
        {
            FinishGroupDrag(commit: true);
        }

        e.Handled = true;
    }

    private void GroupHeader_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isCompletingGroupDrag || !ReferenceEquals(sender, _groupDragHeader))
        {
            return;
        }

        if (_groupDragTransaction is null)
        {
            ClearGroupDragState();
        }
        else
        {
            FinishGroupDrag(commit: false);
        }
    }

    private bool BeginGroupDrag()
    {
        if (_groupDragGroup is null
            || _groupDragHeader is null
            || GetGroupContainer(_groupDragGroup) is not { } container)
        {
            return false;
        }

        var groupColumns = CurrentGroupColumnCount();
        if (EnsureGroupGridCoordinates())
        {
            RefreshGroupPanelLayout();
            TileLayoutStore.Save(TileLayout);
        }

        var layoutOrigin = GetGroupLayoutPosition(container);
        var visibleOrigin = container.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point());
        _groupDragAnchor = _groupDragStart - visibleOrigin;
        _groupDragContainer = container;
        _groupDragTransform = new TranslateTransform(
            visibleOrigin.X - layoutOrigin.X,
            visibleOrigin.Y - layoutOrigin.Y);
        // Keep hit testing anchored to the pre-drag slots. If these bounds follow each
        // preview reorder, a stationary pointer can alternate between two insertion targets.
        var existingTargets = TileLayout.Groups
            .Select(group => (group, container: GetGroupContainer(group)))
            .Where(item => item.container is not null)
            .Select(item => new TileGroupDropTarget(
                item.group.GroupColumn,
                item.group.GroupRow,
                new System.Windows.Rect(
                    GetGroupLayoutPosition(item.container!),
                    new System.Windows.Size(item.container!.ActualWidth, item.container.ActualHeight)),
                ColumnSpan: item.group.WidthUnits))
            .ToArray();
        _groupDragTargets = TileGroupDropResolver.IncludeEmptyColumns(
            existingTargets,
            groupColumns,
            _groupDragGroup.WidthUnits);
        _groupDragTransaction = new TileGroupDragTransaction(TileLayout, _groupDragGroup, groupColumns);
        _isInternalGroupDrag = true;
        _groupDragHeader.SetDragging(true);
        _groupDragContainer.RenderTransform = _groupDragTransform;
        _groupDragContainer.Opacity = 0.96;
        System.Windows.Controls.Panel.SetZIndex(_groupDragContainer, 1000);
        return true;
    }

    private void UpdateGroupDrag(System.Windows.Point position)
    {
        if (_groupDragGroup is null
            || _groupDragTransaction is null
            || _groupDragContainer is null
            || _groupDragTransform is null)
        {
            return;
        }

        if (_groupDragTargets.Length > 0)
        {
            var targetCell = TileGroupDropResolver.ResolveTargetCell(position, _groupDragTargets);
            if (_groupDragTargetCell != targetCell)
            {
                _groupDragTargetCell = targetCell;
                var previousPositions = CaptureGroupReorderPositions();
                if (_groupDragTransaction.Preview(targetCell))
                {
                    RefreshGroupPanelLayout();
                    AnimateGroupReorderFrom(previousPositions);
                }
            }
        }

        var origin = GetGroupLayoutPosition(_groupDragContainer);
        var offset = position - _groupDragAnchor - origin;
        _groupDragTransform.X = offset.X;
        _groupDragTransform.Y = offset.Y;
    }

    private void FinishGroupDrag(bool commit)
    {
        if (_isCompletingGroupDrag)
        {
            return;
        }

        _isCompletingGroupDrag = true;
        var header = _groupDragHeader;
        var container = _groupDragContainer;
        var transaction = _groupDragTransaction;
        var visiblePosition = container is null
            ? new System.Windows.Point()
            : container.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point());
        var changed = false;

        if (transaction is not null)
        {
            if (commit)
            {
                changed = transaction.Commit();
            }
            else
            {
                var previousPositions = CaptureGroupReorderPositions();
                changed = transaction.Cancel();
                if (changed)
                {
                    RefreshGroupPanelLayout();
                    AnimateGroupReorderFrom(previousPositions);
                }
            }
        }

        header?.SetDragging(false);
        if (container is not null)
        {
            container.Opacity = 1;
            System.Windows.Controls.Panel.SetZIndex(container, 0);
            container.RenderTransform = null;
            var finalPosition = GetGroupLayoutPosition(container);
            if (SystemParameters.ClientAreaAnimation)
            {
                Win10ReorderMotion.AnimateFrom(container, visiblePosition - finalPosition);
            }
        }

        ClearGroupDragState();
        header?.ReleaseMouseCapture();
        if (commit && changed)
        {
            TileLayoutStore.Save(TileLayout);
        }

        _isCompletingGroupDrag = false;
    }

    private void CancelPendingGroupDrag()
    {
        if (_groupDragTransaction is not null)
        {
            FinishGroupDrag(commit: false);
            return;
        }

        var header = _groupDragHeader;
        _isCompletingGroupDrag = true;
        ClearGroupDragState();
        header?.ReleaseMouseCapture();
        _isCompletingGroupDrag = false;
    }

    private Dictionary<TileGroup, System.Windows.Point> CaptureGroupReorderPositions()
    {
        return TileLayout.Groups
            .Where(group => !ReferenceEquals(group, _groupDragGroup))
            .Select(group => (group, container: GetGroupContainer(group)))
            .Where(item => item.container is not null)
            .ToDictionary(
                item => item.group,
                item => item.container!.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point()));
    }

    private HashSet<TileGroup> AnimateGroupReorderFrom(
        IReadOnlyDictionary<TileGroup, System.Windows.Point> previousPositions)
    {
        var movedGroups = new HashSet<TileGroup>();
        if (!SystemParameters.ClientAreaAnimation)
        {
            return movedGroups;
        }

        foreach (var (group, previous) in previousPositions)
        {
            if (GetGroupContainer(group) is { } container)
            {
                var delta = previous - GetGroupLayoutPosition(container);
                if (Math.Abs(delta.X) >= 0.1 || Math.Abs(delta.Y) >= 0.1)
                {
                    movedGroups.Add(group);
                    Win10ReorderMotion.AnimateFrom(container, delta);
                }
            }
        }

        return movedGroups;
    }

    private FrameworkElement? GetGroupContainer(TileGroup group)
    {
        return TileGroupsControl.ItemContainerGenerator.ContainerFromItem(group) as FrameworkElement;
    }

    private System.Windows.Point GetGroupLayoutPosition(FrameworkElement container)
    {
        var offset = VisualTreeHelper.GetOffset(container);
        return VisualTreeHelper.GetParent(container) is Visual parent
            ? parent.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point()) + offset
            : new System.Windows.Point(offset.X, offset.Y);
    }

    private void ClearGroupDragState()
    {
        _groupDragGroup = null;
        _groupDragHeader = null;
        _groupDragTransaction = null;
        _groupDragContainer = null;
        _groupDragTransform = null;
        _groupDragTargets = [];
        _groupDragTargetCell = null;
        _isInternalGroupDrag = false;
    }

    private void TileButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCompletingInternalTileDrag)
        {
            e.Handled = true;
            return;
        }

        _dragCompleted = false;
        _dragStart = e.GetPosition(this);
        _dragAnchor = sender is Button button ? e.GetPosition(button) : new System.Windows.Point();
        _tileReflowStability.Reset();
        ResetFolderDropState();
        _dragSourceElement = sender as Button;
        _dragTile = _dragSourceElement?.Tag as TileItem;
        _dragSource = null;
        _dragSourceFolder = null;
        if (_dragTile is not null)
        {
            FindTileLocation(_dragTile, out _dragSource, out _dragSourceFolder);
        }
    }

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isInternalAppDrag)
        {
            EndInternalAppDrag(commit: true, e.GetPosition(MainSurface));
            e.Handled = true;
            return;
        }

        if (_isInternalTileDrag)
        {
            _internalDropIsValid = UpdateInternalTileDrag(e.GetPosition(MainSurface), force: true);
            EndInternalTileDrag(commit: _internalDropIsValid);
            e.Handled = true;
            return;
        }

        if (_dragTransaction is null && !_dragCompleted)
        {
            ClearTileDragState();
        }
    }

    private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isInternalAppDrag)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndInternalAppDrag(commit: false, e.GetPosition(MainSurface));
                return;
            }

            MoveAppDragPreview(e.GetPosition(MainSurface));
            e.Handled = true;
            return;
        }

        if (_isInternalTileDrag)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndInternalTileDrag(commit: false);
                return;
            }

            var position = e.GetPosition(MainSurface);
            MoveInternalDragPreview(position);
            _internalDropIsValid = UpdateInternalTileDrag(position, force: false);
            UpdateTileDragAutoScroll(position);
            e.Handled = true;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed
            || _dragTile is null
            || _dragSource is null
            || _dragSourceElement is null
            || _dragTransaction is not null)
        {
            return;
        }

        var positionInWindow = e.GetPosition(this);
        var delta = positionInWindow - _dragStart;
        if (delta.LengthSquared <= 9)
        {
            return;
        }

        BeginInternalTileDrag(e.GetPosition(MainSurface));
        e.Handled = true;
    }

    private void BeginInternalTileDrag(System.Windows.Point position)
    {
        if (_dragTile is null || _dragSource is null || _dragSourceElement is null)
        {
            return;
        }

        var groupColumns = CurrentGroupColumnCount();
        if (EnsureGroupGridCoordinates())
        {
            UpdateLayout();
            TileLayoutStore.Save(TileLayout);
        }

        ShowInternalDragPreview(
            CaptureElement(_dragSourceElement),
            _dragSourceElement.ActualWidth,
            _dragSourceElement.ActualHeight);
        MoveInternalDragPreview(position);
        _tileDragHitGeometry = new TileDragHitGeometry(CaptureTileAreaDropZones());
        _dragTransaction = new TileDragTransaction(
            TileLayout,
            _dragSource,
            _dragSourceFolder,
            _dragTile,
            groupColumns);
        _dragTile.IsDragging = true;
        _dragCompleted = true;
        _isInternalTileDrag = true;
        _internalDropIsValid = UpdateInternalTileDrag(position, force: false);
        UpdateTileDragAutoScroll(position);
        Mouse.Capture(this, CaptureMode.SubTree);
    }

    private void MoveInternalDragPreview(System.Windows.Point position)
    {
        MoveInternalDragPreview(position, _dragAnchor);
    }

    private void MoveInternalDragPreview(System.Windows.Point position, System.Windows.Point anchor)
    {
        InternalDragPreviewTransform.X = position.X - anchor.X;
        InternalDragPreviewTransform.Y = position.Y - anchor.Y;
    }

    private void UpdateTileDragAutoScroll(System.Windows.Point position)
    {
        _lastInternalTileDragPosition = position;
        var pointer = MainSurface.TranslatePoint(position, TileScrollViewer);
        _tileDragAutoScrollVelocity = pointer.X >= 0 && pointer.X < TileScrollViewer.ActualWidth
            ? TileDragAutoScroll.GetVelocity(
                pointer.Y,
                TileScrollViewer.ActualHeight,
                TileScrollViewer.VerticalOffset,
                TileScrollViewer.ScrollableHeight)
            : 0;

        if (Math.Abs(_tileDragAutoScrollVelocity) < 0.1)
        {
            StopTileDragAutoScroll();
        }
        else if (!_tileDragAutoScrollSubscribed)
        {
            _tileDragAutoScrollLastFrame = null;
            CompositionTarget.Rendering += TileDragAutoScroll_Rendering;
            _tileDragAutoScrollSubscribed = true;
        }
    }

    private void TileDragAutoScroll_Rendering(object? sender, EventArgs e)
    {
        if (!_isInternalTileDrag
            || _dragTransaction is null
            || _dragTile is null
            || e is not RenderingEventArgs rendering)
        {
            StopTileDragAutoScroll();
            return;
        }

        if (_tileDragAutoScrollLastFrame is not { } previousFrame)
        {
            _tileDragAutoScrollLastFrame = rendering.RenderingTime;
            return;
        }

        var elapsedSeconds = Math.Clamp((rendering.RenderingTime - previousFrame).TotalSeconds, 0, 0.05);
        _tileDragAutoScrollLastFrame = rendering.RenderingTime;
        var nextOffset = TileDragAutoScroll.GetNextOffset(
            TileScrollViewer.VerticalOffset,
            TileScrollViewer.ScrollableHeight,
            _tileDragAutoScrollVelocity,
            elapsedSeconds);
        if (Math.Abs(nextOffset - TileScrollViewer.VerticalOffset) < 0.1)
        {
            UpdateTileDragAutoScroll(_lastInternalTileDragPosition);
            return;
        }

        TileScrollViewer.ScrollToVerticalOffset(nextOffset);
        TileScrollViewer.UpdateLayout();
        _internalDropIsValid = UpdateInternalTileDrag(_lastInternalTileDragPosition, force: false);
        UpdateTileDragAutoScroll(_lastInternalTileDragPosition);
    }

    private void StopTileDragAutoScroll()
    {
        _tileDragAutoScrollVelocity = 0;
        _tileDragAutoScrollLastFrame = null;
        if (!_tileDragAutoScrollSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering -= TileDragAutoScroll_Rendering;
        _tileDragAutoScrollSubscribed = false;
    }

    private bool UpdateInternalTileDrag(System.Windows.Point position, bool force)
    {
        if (_dragTransaction is null || _dragTile is null)
        {
            return false;
        }

        var panePosition = MainSurface.TranslatePoint(position, TilePane);
        if (panePosition.X < 0
            || panePosition.Y < 0
            || panePosition.X >= TilePane.ActualWidth
            || panePosition.Y >= TilePane.ActualHeight)
        {
            ResetPendingTileDrop();
            return false;
        }

        var groupsPosition = MainSurface.TranslatePoint(position, TileGroupsControl);
        if (TryFindExpandedFolderAt(groupsPosition, out var folderControl, out var folder, out var group))
        {
            ResetFolderDropState();
            return PreviewFolderRegionDrop(
                group,
                folder,
                TileGroupsControl.TranslatePoint(groupsPosition, folderControl),
                _dragTile,
                force);
        }

        var draggedBounds = new System.Windows.Rect(
            groupsPosition.X - _dragAnchor.X,
            groupsPosition.Y - _dragAnchor.Y,
            _dragTile.PixelWidth,
            _dragTile.PixelHeight);
        if (TryResolveTileAreaGroup(groupsPosition, out var target, out var groupControl, draggedBounds))
        {
            return PreviewTileDrop(
                target,
                TileGroupsControl.TranslatePoint(groupsPosition, groupControl),
                _dragTile,
                force);
        }

        ResetPendingTileDrop();
        var newGroupTarget = ResolveNewTileGroupTarget(draggedBounds);
        if (_dragTransaction.Intent != TileDropIntent.NewGroup
            || _dragTransaction.PreviewTarget is null
            || Win10GroupGridLayout.GetCell(_dragTransaction.PreviewTarget)
            != new TileGroupCell(newGroupTarget.GroupColumn, newGroupTarget.GroupRow)
            || _dragTile.Column != newGroupTarget.TileColumn
            || _dragTile.Row != newGroupTarget.TileRow)
        {
            var previousPositions = CaptureReorderPositions();
            var previousGroupPositions = CaptureGroupReorderPositions();
            _dragTransaction.PreviewNewGroup(newGroupTarget);
            UpdateLayout();
            var movedGroups = AnimateGroupReorderFrom(previousGroupPositions);
            AnimateReorderFrom(previousPositions, movedGroups);
        }

        return true;
    }

    private bool TryFindExpandedFolderAt(
        System.Windows.Point pointer,
        out ItemsControl control,
        out TileItem folder,
        out TileGroup group)
    {
        foreach (var candidate in FindVisualDescendants<ItemsControl>(TileGroupsControl)
                     .Where(item => item.Tag is TileItem { IsTileFolder: true, IsFolderExpanded: true }))
        {
            var origin = candidate.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point());
            if (pointer.X < origin.X
                || pointer.X >= origin.X + candidate.ActualWidth
                || pointer.Y < origin.Y
                || pointer.Y >= origin.Y + candidate.ActualHeight)
            {
                continue;
            }

            folder = (TileItem)candidate.Tag;
            if (FindTileLocation(folder, out group, out _))
            {
                control = candidate;
                return true;
            }
        }

        control = null!;
        folder = null!;
        group = null!;
        return false;
    }

    private void EndInternalTileDrag(bool commit)
    {
        if (_isCompletingInternalTileDrag)
        {
            return;
        }

        var transaction = _dragTransaction;
        var tile = _dragTile;
        var didCommit = transaction is not null && commit && transaction.PreviewTarget is not null;
#if DEBUG
        if (transaction is not null)
        {
            DiagnosticLog.Write(
                $"tile-drop end commitRequested={commit} didCommit={didCommit} " +
                $"tile={tile?.Name ?? "<null>"} intent={transaction.Intent} " +
                $"target={transaction.PreviewTarget?.Name ?? "<null>"}");
        }
#endif
        var rollbackPositions = didCommit ? null : CaptureReorderPositions();
        if (didCommit)
        {
            transaction!.Commit();
            TileLayoutStore.Save(TileLayout);
        }

        transaction?.Dispose();
        if (!didCommit && rollbackPositions is not null)
        {
            UpdateLayout();
            AnimateReorderFrom(rollbackPositions);
        }

        Mouse.Capture(null);
        _dragTransaction = null;
        _tileDragHitGeometry = null;
        _isInternalTileDrag = false;
        _internalDropIsValid = false;
        StopTileDragAutoScroll();
        _suppressTileActivationUntil = Environment.TickCount64 + 300;
        ResetPendingTileDrop();
        ClearTileDragState();

        if (tile is null
            || InternalDragPreview.Visibility != Visibility.Visible
            || !SystemParameters.ClientAreaAnimation)
        {
            CompleteInternalTileDragVisual(tile);
            return;
        }

        _isCompletingInternalTileDrag = true;
        if (didCommit)
        {
            AnimateInternalDragPreviewHandoff(tile);
        }
        else
        {
            AnimateInternalDragPreviewReturn(tile);
        }
    }

    private void AnimateInternalDragPreviewHandoff(TileItem tile)
    {
        var duration = TimeSpan.FromMilliseconds(Win10ReorderMotion.DropHandoffDurationMilliseconds);
        var animation = Win10ReorderMotion.Create(InternalDragPreview.Opacity, 0, duration);
        animation.Completed += (_, _) => CompleteInternalTileDragVisual(tile);
        InternalDragPreview.BeginAnimation(OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateInternalDragPreviewReturn(TileItem tile)
    {
        UpdateLayout();
        if (FindReorderElement(tile) is not { } target)
        {
            CompleteInternalTileDragVisual(tile);
            return;
        }

        var targetPosition = target.TransformToAncestor(MainSurface).Transform(new System.Windows.Point());
        var duration = TimeSpan.FromMilliseconds(Win10ReorderMotion.CancelReturnDurationMilliseconds);
        var x = Win10ReorderMotion.Create(InternalDragPreviewTransform.X, targetPosition.X, duration);
        var y = Win10ReorderMotion.Create(InternalDragPreviewTransform.Y, targetPosition.Y, duration);
        y.Completed += (_, _) => CompleteInternalTileDragVisual(tile);
        InternalDragPreviewTransform.BeginAnimation(
            TranslateTransform.XProperty,
            x,
            HandoffBehavior.SnapshotAndReplace);
        InternalDragPreviewTransform.BeginAnimation(
            TranslateTransform.YProperty,
            y,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void CompleteInternalTileDragVisual(TileItem? tile)
    {
        if (tile is not null)
        {
            tile.IsDragging = false;
        }

        HideInternalDragPreview();
        _isCompletingInternalTileDrag = false;
    }

    private void ShowInternalDragPreview(BitmapSource source, double width, double height)
    {
        InternalDragPreview.BeginAnimation(OpacityProperty, null);
        InternalDragPreviewTransform.BeginAnimation(TranslateTransform.XProperty, null);
        InternalDragPreviewTransform.BeginAnimation(TranslateTransform.YProperty, null);
        InternalDragPreview.Opacity = 0.96;
        InternalDragPreview.Source = source;
        InternalDragPreview.Width = width;
        InternalDragPreview.Height = height;
        InternalDragPreview.Visibility = Visibility.Visible;
    }

    private void HideInternalDragPreview()
    {
        InternalDragPreview.BeginAnimation(OpacityProperty, null);
        InternalDragPreviewTransform.BeginAnimation(TranslateTransform.XProperty, null);
        InternalDragPreviewTransform.BeginAnimation(TranslateTransform.YProperty, null);
        InternalDragPreview.Opacity = 0.96;
        InternalDragPreview.Source = null;
        InternalDragPreview.Visibility = Visibility.Collapsed;
    }

    private void ClearTileDragState()
    {
        _dragTile = null;
        _dragSource = null;
        _dragSourceFolder = null;
        _dragSourceElement = null;
    }

    private static BitmapSource CaptureElement(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    private void TileGroup_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is ItemsControl { Tag: TileGroup target } itemsControl
            && e.Data.GetData(typeof(TileItem)) is TileItem tile
            && _dragTransaction is not null)
        {
            e.Effects = PreviewTileDrop(target, e.GetPosition(itemsControl), tile, force: false)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }
        else
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void TileGroup_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not ItemsControl { Tag: TileGroup target } itemsControl)
        {
            return;
        }

        var position = e.GetPosition(itemsControl);
        if (e.Data.GetData(typeof(TileItem)) is TileItem tile && _dragTransaction is not null)
        {
            var currentFolderTarget = TileDropResolver.FindFolderTarget(target, tile, position, _dragAnchor);
            var canCommitCurrentFolderPreview = _dragTransaction.PreviewTarget == target
                                                && _dragTransaction.Intent is TileDropIntent.CreateFolder
                                                    or TileDropIntent.AddToFolder
                                                && currentFolderTarget?.IsTileFolder == true;
            if (canCommitCurrentFolderPreview || PreviewTileDrop(target, position, tile, force: true))
            {
                _dragTransaction.Commit();
                TileLayoutStore.Save(TileLayout);
                e.Effects = DragDropEffects.Move;
            }
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddDroppedTiles(target, paths, position);
        }

        e.Handled = true;
    }

    private void FolderRegion_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is ItemsControl { Tag: TileItem folder } itemsControl
            && e.Data.GetData(typeof(TileItem)) is TileItem tile
            && _dragTransaction is not null
            && FindTileLocation(folder, out var group, out _))
        {
            e.Effects = PreviewFolderRegionDrop(group, folder, e.GetPosition(itemsControl), tile, force: false)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void FolderRegion_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is ItemsControl { Tag: TileItem folder } itemsControl
            && e.Data.GetData(typeof(TileItem)) is TileItem tile
            && _dragTransaction is not null
            && FindTileLocation(folder, out var group, out _)
            && PreviewFolderRegionDrop(group, folder, e.GetPosition(itemsControl), tile, force: true))
        {
            _dragTransaction.Commit();
            TileLayoutStore.Save(TileLayout);
            e.Effects = DragDropEffects.Move;
        }

        e.Handled = true;
    }

    private void TileArea_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TileItem)) is TileItem tile && _dragTransaction is not null)
        {
            if (TryResolveTileAreaGroup(e.GetPosition(TileGroupsControl), out var target, out var groupControl))
            {
                e.Effects = PreviewTileDrop(target, e.GetPosition(groupControl), tile, force: false)
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
            }
            else
            {
                ResetPendingTileDrop();
                var previousTarget = _dragTransaction.PreviewTarget;
                var newGroup = _dragTransaction.PreviewNewGroup();
                if (!ReferenceEquals(previousTarget, newGroup))
                {
                    UpdateLayout();
                }

                e.Effects = DragDropEffects.Move;
            }
        }
        else
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void TileArea_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TileItem)) is TileItem tile && _dragTransaction is not null)
        {
            if (TryResolveTileAreaGroup(e.GetPosition(TileGroupsControl), out var target, out var groupControl))
            {
                var position = e.GetPosition(groupControl);
                if (PreviewTileDrop(target, position, tile, force: true))
                {
                    _dragTransaction.Commit();
                    TileLayoutStore.Save(TileLayout);
                    e.Effects = DragDropEffects.Move;
                }
            }
            else if (_dragTransaction.PreviewTarget is not null)
            {
                _dragTransaction.Commit();
                TileLayoutStore.Save(TileLayout);
                e.Effects = DragDropEffects.Move;
            }
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var group = TileGroupManager.Add(TileLayout);
            if (AddDroppedTiles(group, paths, new System.Windows.Point()))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                TileGroupManager.Remove(TileLayout, group);
                e.Effects = DragDropEffects.None;
            }
        }

        e.Handled = true;
    }

    private bool TryResolveTileAreaGroup(
        System.Windows.Point pointer,
        out TileGroup target,
        out ItemsControl groupControl,
        System.Windows.Rect? draggedBounds = null)
    {
        var controls = FindVisualDescendants<ItemsControl>(TileGroupsControl)
            .Where(control => control.Tag is TileGroup)
            .ToArray();
        TileGroupDropZone? resolved;
        if (draggedBounds is { } bounds && _tileDragHitGeometry is not null)
        {
            resolved = _tileDragHitGeometry.FindTarget(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height);
        }
        else
        {
            var zones = controls.Select(control =>
            {
                var group = (TileGroup)control.Tag;
                var origin = control.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point());
                return new TileGroupDropZone(
                    group.Id,
                    origin.X,
                    origin.Y,
                    control.ActualWidth,
                    control.ActualHeight,
                    GroupColumn: group.GroupColumn,
                    GroupRow: group.GroupRow);
            });
            resolved = draggedBounds is { } liveBounds
                ? TileAreaDropResolver.FindTargetForDraggedTile(
                    zones,
                    liveBounds.Left,
                    liveBounds.Top,
                    liveBounds.Width,
                    liveBounds.Height)
                : TileAreaDropResolver.FindTarget(zones, pointer.X, pointer.Y);
        }

        groupControl = controls.FirstOrDefault(control => ((TileGroup)control.Tag).Id == resolved?.GroupId)!;
        target = groupControl?.Tag as TileGroup ?? null!;
        return target is not null;
    }

    private TileGroupDropZone[] CaptureTileAreaDropZones()
    {
        return FindVisualDescendants<ItemsControl>(TileGroupsControl)
            .Where(control => control.Tag is TileGroup)
            .Select(control =>
            {
                var group = (TileGroup)control.Tag;
                var origin = control.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point());
                return new TileGroupDropZone(
                    group.Id,
                    origin.X,
                    origin.Y,
                    control.ActualWidth,
                    control.ActualHeight,
                    GroupColumn: group.GroupColumn,
                    GroupRow: group.GroupRow);
            })
            .ToArray();
    }

    private TileNewGroupDropTarget ResolveNewTileGroupTarget(System.Windows.Rect draggedBounds)
    {
        if (_tileDragHitGeometry is not null)
        {
            return _tileDragHitGeometry.FindNewGroupTarget(
                draggedBounds.Left,
                draggedBounds.Top,
                draggedBounds.Height,
                _dragTile!.Size.ColumnSpan(),
                CurrentGroupColumnCount());
        }

        var zones = CaptureTileAreaDropZones();
        if (_dragTransaction is { Intent: TileDropIntent.NewGroup, PreviewTarget: { } provisional })
        {
            zones = zones
                .Where(zone => zone.GroupId != provisional.Id)
                .ToArray();
        }

        return TileAreaDropResolver.FindNewGroupTargetForDraggedTile(
            zones,
            draggedBounds.Left,
            draggedBounds.Top,
            draggedBounds.Height,
            _dragTile!.Size.ColumnSpan(),
            CurrentGroupColumnCount());
    }

    private int CurrentGroupColumnCount()
    {
        var availableWidth = TileScrollViewer.ViewportWidth;
        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            availableWidth = TilePane.ActualWidth - TileScrollViewer.Margin.Left;
        }

        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            availableWidth = Math.Max(Win10TileMetrics.GroupWidth, Width - 340);
        }

        var widthColumns = Win10GroupWrapPanel.ColumnsForWidth(availableWidth);
        var visualColumns = TileLayout.Groups
            .Select(group => new { Group = group, Container = GetGroupContainer(group) })
            .Where(item => item.Container is not null)
            .Select(item => (int)Math.Round(
                GetGroupLayoutPosition(item.Container!).X / TileWorkspaceMetrics.ColumnPitch)
                            + item.Group.WidthUnits)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(1, Math.Max(widthColumns, visualColumns));
    }

    private bool EnsureTileScrollBarClearance()
    {
        if (TileScrollViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
        {
            return false;
        }

        var scrollBar = FindVisualDescendants<ScrollBar>(TileScrollViewer)
            .FirstOrDefault(candidate => candidate.Orientation == System.Windows.Controls.Orientation.Vertical);
        if (scrollBar is null)
        {
            return false;
        }

        var footprint = scrollBar.ActualWidth + scrollBar.Margin.Left + scrollBar.Margin.Right;
        var viewportWidth = TileScrollViewer.ViewportWidth;
        var columns = Win10GroupWrapPanel.ColumnsForWidth(viewportWidth);
        var deficit = Win10GroupWrapPanel.OverlayClearanceDeficit(viewportWidth, columns, footprint);
        if (deficit < 0.1)
        {
            return false;
        }

        Width += deficit;
        UpdateLayout();
        return true;
    }

    private void RefreshGroupPanelLayout()
    {
        var panel = FindVisualDescendants<Win10GroupWrapPanel>(TileGroupsControl).FirstOrDefault();
        panel?.InvalidateMeasure();
        panel?.InvalidateArrange();
        TileGroupsControl.InvalidateMeasure();
        TileGroupsControl.InvalidateArrange();
        TileGroupsControl.UpdateLayout();
    }

    private bool EnsureGroupGridCoordinates() =>
        Win10GroupGridLayout.EnsureCoordinates(TileLayout, CurrentGroupColumnCount());

    private bool PreviewFolderRegionDrop(
        TileGroup target,
        TileItem folder,
        System.Windows.Point position,
        TileItem tile,
        bool force)
    {
        var (column, row) = TileDropResolver.GetCell(position, _dragAnchor, tile, target.ContentColumns);
        var key = $"folder:{target.Id}:{folder.Id}:{column}:{row}";
        if (!force)
        {
            _pendingDropTarget = target;
            _pendingDropFolder = folder;
            _pendingDropPosition = position;
            _pendingDropTile = tile;
            if (_tileReflowStability.Observe(key, position))
            {
                RestartTileReflowTimer();
            }

            return true;
        }

        var previousPositions = CaptureReorderPositions();
        var previewed = _dragTransaction!.PreviewInsideFolder(target, folder, column, row);
        if (previewed)
        {
            UpdateLayout();
            AnimateReorderFrom(previousPositions);
        }

        return previewed;
    }

    private bool PreviewTileDrop(
        TileGroup target,
        System.Windows.Point position,
        TileItem tile,
        bool force)
    {
        var logicalPosition = new System.Windows.Point(
            position.X,
            TileFolderLayout.ToLogicalY(target, position.Y));
        var (column, row) = TileDropResolver.GetCell(logicalPosition, _dragAnchor, tile, target.ContentColumns);
        var folderTarget = TileDropResolver.FindFolderTarget(
            target,
            tile,
            position,
            _dragAnchor,
            _armedFolderDropTarget);
#if DEBUG
        TraceTileDropGeometry(target, logicalPosition, position, tile, column, row, folderTarget);
#endif
        var key = TileDropResolver.GetStabilityKey(target, column, row, folderTarget);
        if (!force)
        {
            if (folderTarget is not null)
            {
                _tileReflowTimer.Stop();
                _tileReflowStability.Reset();
                if (ReferenceEquals(target, _armedFolderDropGroup)
                    && ReferenceEquals(folderTarget, _armedFolderDropTarget))
                {
                    return true;
                }

                ClearArmedFolderDropTarget();
                _pendingFolderDropGroup = target;
                _pendingFolderDropTarget = folderTarget;
                var restartFolderTimer = _folderActivationStability.Observe(key, position);
#if DEBUG
                if (restartFolderTimer)
                {
                    DiagnosticLog.Write(
                        $"tile-drop folder-candidate pointer=({position.X:F1},{position.Y:F1}) " +
                        $"target={target.Name} folderTarget={folderTarget.Name}");
                }
#endif
                if (restartFolderTimer)
                {
                    _folderActivationTimer.Stop();
                    _folderActivationTimer.Start();
                }

                return true;
            }

            ResetFolderDropState();
            _pendingDropTarget = target;
            _pendingDropFolder = null;
            _pendingDropPosition = position;
            _pendingDropTile = tile;
            var restartTimer = _tileReflowStability.Observe(key, position);
#if DEBUG
            if (restartTimer)
            {
                var keyChanged = _tileDropTraceCandidateKey != key;
                var distance = _tileDropTraceCandidateKey is null
                    ? 0
                    : (position - _tileDropTraceCandidatePosition).Length;
                DiagnosticLog.Write(
                    $"tile-drop candidate keyChanged={keyChanged} restartDistance={distance:F1} " +
                    $"pointer=({position.X:F1},{position.Y:F1}) cell=({column},{row}) " +
                    $"target={target.Name} folderTarget={folderTarget?.Name ?? "<null>"} " +
                    $"folderKind={(folderTarget is null ? "none" : folderTarget.IsTileFolder ? "existing" : "tile")}");
                _tileDropTraceCandidateKey = key;
                _tileDropTraceCandidatePosition = position;
            }
#endif
            if (restartTimer)
            {
                RestartTileReflowTimer();
            }

            return true;
        }

        var previousPositions = CaptureReorderPositions();
        var commitFolderTarget = ReferenceEquals(target, _armedFolderDropGroup)
                                 && ReferenceEquals(folderTarget, _armedFolderDropTarget)
            ? folderTarget
            : null;
#if DEBUG
        var requestedIntent = commitFolderTarget is null
            ? TileDropIntent.Reposition
            : commitFolderTarget.IsTileFolder
                ? TileDropIntent.AddToFolder
                : TileDropIntent.CreateFolder;
        DiagnosticLog.Write(
            $"tile-drop timer-force pointer=({position.X:F1},{position.Y:F1}) cell=({column},{row}) " +
            $"target={target.Name} folderTarget={commitFolderTarget?.Name ?? "<null>"} requested={requestedIntent}");
#endif
        ClearArmedFolderDropTarget();
        var previewed = commitFolderTarget is not null
            ? _dragTransaction!.PreviewFolder(target, commitFolderTarget)
            : _dragTransaction!.Preview(target, column, row);
#if DEBUG
        DiagnosticLog.Write(
            $"tile-drop preview-result success={previewed} actual={_dragTransaction!.Intent} " +
            $"previewTarget={_dragTransaction.PreviewTarget?.Name ?? "<null>"}");
#endif
        if (previewed)
        {
            UpdateLayout();
            AnimateReorderFrom(previousPositions);
        }

        return previewed;
    }

    private void RestartTileReflowTimer()
    {
        _tileReflowTimer.Stop();
        _tileReflowTimer.Start();
    }

    private void FolderActivationTimer_Tick(object? sender, EventArgs e)
    {
        _folderActivationTimer.Stop();
        if (_dragTransaction is null
            || _pendingFolderDropGroup is null
            || _pendingFolderDropTarget is null
            || !_pendingFolderDropGroup.Tiles.Contains(_pendingFolderDropTarget))
        {
            ResetFolderDropState();
            return;
        }

        ClearArmedFolderDropTarget();
        _armedFolderDropGroup = _pendingFolderDropGroup;
        _armedFolderDropTarget = _pendingFolderDropTarget;
        _armedFolderDropTarget.IsFolderDropTarget = true;
#if DEBUG
        DiagnosticLog.Write(
            $"tile-drop folder-armed target={_armedFolderDropGroup.Name} " +
            $"folderTarget={_armedFolderDropTarget.Name}");
#endif
    }

    private void ClearArmedFolderDropTarget()
    {
        if (_armedFolderDropTarget is not null)
        {
            _armedFolderDropTarget.IsFolderDropTarget = false;
        }

        _armedFolderDropGroup = null;
        _armedFolderDropTarget = null;
    }

    private void ResetFolderDropState()
    {
        _folderActivationTimer.Stop();
        _folderActivationStability.Reset();
        _pendingFolderDropGroup = null;
        _pendingFolderDropTarget = null;
        ClearArmedFolderDropTarget();
    }

    private void TileReflowTimer_Tick(object? sender, EventArgs e)
    {
        _tileReflowTimer.Stop();
        if (_dragTransaction is null || _pendingDropTarget is null || _pendingDropTile is null)
        {
            return;
        }

        if (_pendingDropFolder is not null)
        {
            PreviewFolderRegionDrop(
                _pendingDropTarget,
                _pendingDropFolder,
                _pendingDropPosition,
                _pendingDropTile,
                force: true);
        }
        else
        {
            PreviewTileDrop(_pendingDropTarget, _pendingDropPosition, _pendingDropTile, force: true);
        }
    }

    private void ResetPendingTileDrop()
    {
#if DEBUG
        if (_tileDropTraceCandidateKey is not null)
        {
            DiagnosticLog.Write("tile-drop candidate-reset");
            _tileDropTraceCandidateKey = null;
            _tileDropTraceCandidatePosition = default;
        }

        _tileDropGeometryTraceSignature = null;
#endif
        _tileReflowTimer.Stop();
        _tileReflowStability.Reset();
        ResetFolderDropState();
        _pendingDropTarget = null;
        _pendingDropFolder = null;
        _pendingDropTile = null;
    }

#if DEBUG
    private void TraceTileDropGeometry(
        TileGroup target,
        System.Windows.Point logicalPosition,
        System.Windows.Point displayPosition,
        TileItem moving,
        int column,
        int row,
        TileItem? folderTarget)
    {
        var draggedBounds = new System.Windows.Rect(
            logicalPosition.X - _dragAnchor.X,
            logicalPosition.Y - _dragAnchor.Y,
            moving.PixelWidth,
            moving.PixelHeight);
        var draggedCenter = new System.Windows.Point(
            draggedBounds.Left + draggedBounds.Width / 2,
            draggedBounds.Top + draggedBounds.Height / 2);
        var tiles = target.Tiles
            .Where(tile => !ReferenceEquals(tile, moving))
            .Select(tile =>
                (Tile: tile,
                    Bounds: new System.Windows.Rect(tile.Left, tile.Top, tile.PixelWidth, tile.PixelHeight)))
            .ToArray();
        var bodyHit = tiles.FirstOrDefault(candidate => candidate.Bounds.Contains(draggedCenter));
        var left = tiles
            .Where(candidate => candidate.Bounds.Right <= draggedBounds.Left
                                && candidate.Bounds.Top < draggedBounds.Bottom
                                && candidate.Bounds.Bottom > draggedBounds.Top)
            .OrderBy(candidate => draggedBounds.Left - candidate.Bounds.Right)
            .FirstOrDefault();
        var right = tiles
            .Where(candidate => candidate.Bounds.Left >= draggedBounds.Right
                                && candidate.Bounds.Top < draggedBounds.Bottom
                                && candidate.Bounds.Bottom > draggedBounds.Top)
            .OrderBy(candidate => candidate.Bounds.Left - draggedBounds.Right)
            .FirstOrDefault();
        var above = tiles
            .Where(candidate => candidate.Bounds.Bottom <= draggedBounds.Top
                                && candidate.Bounds.Left < draggedBounds.Right
                                && candidate.Bounds.Right > draggedBounds.Left)
            .OrderBy(candidate => draggedBounds.Top - candidate.Bounds.Bottom)
            .FirstOrDefault();
        var below = tiles
            .Where(candidate => candidate.Bounds.Top >= draggedBounds.Bottom
                                && candidate.Bounds.Left < draggedBounds.Right
                                && candidate.Bounds.Right > draggedBounds.Left)
            .OrderBy(candidate => candidate.Bounds.Top - draggedBounds.Bottom)
            .FirstOrDefault();
        var signature = string.Join(
            '|',
            target.Id,
            column,
            row,
            bodyHit.Tile?.Id,
            folderTarget?.Id,
            left.Tile?.Id,
            right.Tile?.Id,
            above.Tile?.Id,
            below.Tile?.Id);
        if (_tileDropGeometryTraceSignature == signature)
        {
            return;
        }

        _tileDropGeometryTraceSignature = signature;
        DiagnosticLog.Write(
            $"[DEBUG-tile-drop-geometry] pointer=({displayPosition.X:F1},{displayPosition.Y:F1}) " +
            $"logical=({logicalPosition.X:F1},{logicalPosition.Y:F1}) anchor=({_dragAnchor.X:F1},{_dragAnchor.Y:F1}) " +
            $"drag={FormatBounds(draggedBounds)} center=({draggedCenter.X:F1},{draggedCenter.Y:F1}) " +
            $"cell=({column},{row}) body={FormatNeighbor(bodyHit, draggedBounds, Axis.None)} " +
            $"folder={folderTarget?.Name ?? "<null>"} left={FormatNeighbor(left, draggedBounds, Axis.Horizontal)} " +
            $"right={FormatNeighbor(right, draggedBounds, Axis.Horizontal)} " +
            $"above={FormatNeighbor(above, draggedBounds, Axis.Vertical)} " +
            $"below={FormatNeighbor(below, draggedBounds, Axis.Vertical)}");
    }

    private static string FormatNeighbor(
        (TileItem? Tile, System.Windows.Rect Bounds) candidate,
        System.Windows.Rect draggedBounds,
        Axis axis)
    {
        if (candidate.Tile is null)
        {
            return "<null>";
        }

        var gap = axis switch
        {
            Axis.Horizontal when candidate.Bounds.Right <= draggedBounds.Left =>
                draggedBounds.Left - candidate.Bounds.Right,
            Axis.Horizontal => candidate.Bounds.Left - draggedBounds.Right,
            Axis.Vertical when candidate.Bounds.Bottom <= draggedBounds.Top =>
                draggedBounds.Top - candidate.Bounds.Bottom,
            Axis.Vertical => candidate.Bounds.Top - draggedBounds.Bottom,
            _ => 0,
        };
        return $"{candidate.Tile.Name}:{FormatBounds(candidate.Bounds)}:gap={gap:F1}";
    }

    private static string FormatBounds(System.Windows.Rect bounds) =>
        $"({bounds.Left:F1},{bounds.Top:F1},{bounds.Right:F1},{bounds.Bottom:F1})";

    private enum Axis
    {
        None,
        Horizontal,
        Vertical,
    }
#endif
    private bool AddDroppedTiles(TileGroup target, IEnumerable<string> paths, System.Windows.Point position)
    {
        var added = false;
        foreach (var path in paths)
        {
            var tile = DroppedTileFactory.Create(path);
            if (tile is null)
            {
                continue;
            }

            (int Column, int Row) location;
            if (added)
            {
                if (!Win10GroupLayout.TryFindFirstAvailable(target, tile, out location))
                {
                    continue;
                }
            }
            else
            {
                location = (
                    Math.Clamp((int)Math.Round(position.X / Win10TileMetrics.CellPitch), 0,
                        target.ContentColumns - tile.Size.ColumnSpan()),
                    Math.Max(0, (int)Math.Round(position.Y / Win10TileMetrics.CellPitch)));
            }

            added |= Win10GroupLayout.Add(target, tile, location.Column, location.Row);
        }

        if (added)
        {
            TileLayoutStore.Save(TileLayout);
        }

        return added;
    }

    private Dictionary<TileItem, System.Windows.Point> CaptureReorderPositions()
    {
        var positions = new Dictionary<TileItem, System.Windows.Point>();
        foreach (var group in TileLayout.Groups)
        {
            var groupControl = FindVisualDescendants<ItemsControl>(TileGroupsControl)
                .FirstOrDefault(control => ReferenceEquals(control.Tag, group));
            if (groupControl is null)
            {
                continue;
            }

            foreach (var tile in group.Tiles)
            {
                CaptureReorderPosition(tile, groupControl.ItemContainerGenerator.ContainerFromItem(tile), positions);
                if (!tile.IsTileFolder || !tile.IsFolderExpanded)
                {
                    continue;
                }

                var folderControl = FindVisualDescendants<ItemsControl>(TileGroupsControl)
                    .FirstOrDefault(control => ReferenceEquals(control.Tag, tile));
                if (folderControl is null)
                {
                    continue;
                }

                foreach (var child in tile.FolderTiles)
                {
                    CaptureReorderPosition(child, folderControl.ItemContainerGenerator.ContainerFromItem(child),
                        positions);
                }
            }
        }

        return positions;
    }

    private void CaptureReorderPosition(
        TileItem tile,
        DependencyObject? candidate,
        IDictionary<TileItem, System.Windows.Point> positions)
    {
        if (candidate is FrameworkElement { IsVisible: true } element
            && element.IsDescendantOf(TileGroupsControl))
        {
            positions[tile] = element.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point());
        }
    }

    private void AnimateReorderFrom(
        IReadOnlyDictionary<TileItem, System.Windows.Point> previousPositions,
        IReadOnlySet<TileGroup>? groupsAnimatedAsContainers = null)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        foreach (var (tile, previous) in previousPositions)
        {
            if (groupsAnimatedAsContainers is not null
                && FindTileLocation(tile, out var group, out _)
                && groupsAnimatedAsContainers.Contains(group))
            {
                continue;
            }

            if (FindReorderElement(tile) is not { } element)
            {
                continue;
            }

            var current = element.TransformToAncestor(TileGroupsControl).Transform(new System.Windows.Point());
            var activeTranslation = element.RenderTransform is TranslateTransform transform
                ? new System.Windows.Vector(transform.X, transform.Y)
                : new System.Windows.Vector();
            var delta = Win10ReorderMotion.ResolveRetargetDelta(previous, current, activeTranslation);
            Win10ReorderMotion.AnimateFrom(element, delta);
        }
    }

    private FrameworkElement? FindReorderElement(TileItem tile)
    {
        if (!FindTileLocation(tile, out var group, out var folder))
        {
            return null;
        }

        var itemsControl = folder is null
            ? FindVisualDescendants<ItemsControl>(TileGroupsControl)
                .FirstOrDefault(control => ReferenceEquals(control.Tag, group))
            : FindVisualDescendants<ItemsControl>(TileGroupsControl)
                .FirstOrDefault(control => ReferenceEquals(control.Tag, folder));
        return itemsControl?.ItemContainerGenerator.ContainerFromItem(tile) as FrameworkElement;
    }
}
