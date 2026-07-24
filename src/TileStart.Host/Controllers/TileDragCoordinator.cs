using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Image = System.Windows.Controls.Image;
using ItemsControl = System.Windows.Controls.ItemsControl;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Panel = System.Windows.Controls.Panel;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Size = System.Windows.Size;
using Vector = System.Windows.Vector;
using TileStart.Host.Applications;
using TileStart.Host.Persistence;
using TileStart.Host.Tiles.DragDrop;
using TileStart.Host.Tiles.Layout;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Utilities;

namespace TileStart.Host.Controllers;

internal delegate bool FindTileLocationHandler(TileItem tile, out TileGroup group, out TileItem? folder);

internal sealed class TileDragCoordinator
{
    private readonly Window _window;
    private readonly Grid _mainSurface;
    private readonly Border _tilePane;
    private readonly ScrollViewer _tileScrollViewer;
    private readonly ItemsControl _tileGroupsControl;
    private readonly Image _internalDragPreview;
    private readonly TranslateTransform _internalDragPreviewTransform;
    private readonly TileLayout _tileLayout;
    private readonly ApplicationPaneController _appController;
    private readonly FindTileLocationHandler _findTileLocation;
    private readonly Action<long> _setSuppressTileActivationUntil;
    private readonly Func<FrameworkElement, BitmapSource> _captureElement;

    private Point _dragStart;
    private Point _appDragStart;
    private Point _appDragAnchor;

    private readonly DispatcherTimer _tileReflowTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(TileDropResolver.ReflowDelayMilliseconds),
    };

    private readonly DispatcherTimer _folderActivationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(TileDropResolver.FolderActivationDelayMilliseconds),
    };

    private readonly TileReflowStability _folderActivationStability = new(TileDropResolver.FolderActivationDrift);
    private bool _tileDragAutoScrollSubscribed;
    private double _tileDragAutoScrollVelocity;
    private TimeSpan? _tileDragAutoScrollLastFrame;
    private Point _lastInternalTileDragPosition;
    private Point _dragAnchor;
    private TileGroup? _pendingDropTarget;
    private TileItem? _pendingDropFolder;
    private Point _pendingDropPosition;
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
    private Point _groupDragStart;
    private Vector _groupDragAnchor;
    private TileGroup? _groupDragGroup;
    private TileGroupHeader? _groupDragHeader;
    private TileGroupDragTransaction? _groupDragTransaction;
    private FrameworkElement? _groupDragContainer;
    private TranslateTransform? _groupDragTransform;
    private TileGroupDropTarget[] _groupDragTargets = [];
    private TileGroupCell? _groupDragTargetCell;
    private bool _isInternalGroupDrag;
    private bool _isCompletingGroupDrag;
    private readonly TileReflowStability _tileReflowStability = new();
#if DEBUG
    private string? _tileDropTraceCandidateKey;
    private Point _tileDropTraceCandidatePosition;
    private string? _tileDropGeometryTraceSignature;
#endif

    public TileDragCoordinator(
        Window window,
        Grid mainSurface,
        Border tilePane,
        ScrollViewer tileScrollViewer,
        ItemsControl tileGroupsControl,
        Image internalDragPreview,
        TranslateTransform internalDragPreviewTransform,
        TileLayout tileLayout,
        ApplicationPaneController appController,
        FindTileLocationHandler findTileLocation,
        Action<long> setSuppressTileActivationUntil,
        Func<FrameworkElement, BitmapSource> captureElement)
    {
        _window = window;
        _mainSurface = mainSurface;
        _tilePane = tilePane;
        _tileScrollViewer = tileScrollViewer;
        _tileGroupsControl = tileGroupsControl;
        _internalDragPreview = internalDragPreview;
        _internalDragPreviewTransform = internalDragPreviewTransform;
        _tileLayout = tileLayout;
        _appController = appController;
        _findTileLocation = findTileLocation;
        _setSuppressTileActivationUntil = setSuppressTileActivationUntil;
        _captureElement = captureElement;

        _tileReflowTimer.Tick += TileReflowTimer_Tick;
        _folderActivationTimer.Tick += FolderActivationTimer_Tick;
    }

    public bool IsDragging => _isInternalTileDrag || _isInternalAppDrag || _isInternalGroupDrag;

    public bool DragCompletedFlag => _dragCompleted;

    public void ResetDragCompletedFlag() => _dragCompleted = false;

    public event Action<bool>? DragCompleted;

    public bool CancelCurrentDrag()
    {
        if (_isInternalAppDrag)
        {
            EndInternalAppDrag(commit: false, Mouse.GetPosition(_mainSurface));
            return true;
        }

        if (_isInternalTileDrag)
        {
            EndInternalTileDrag(commit: false);
            return true;
        }

        if (_isInternalGroupDrag || _groupDragTransaction is not null)
        {
            FinishGroupDrag(commit: false);
            return true;
        }

        return false;
    }

    public void StopTileDragAutoScroll()
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

    // =============================================
    // App drag
    // =============================================

    public void AppButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        _appDragStart = e.GetPosition(_window);
        _appDragAnchor = _appDragSourceElement is null
            ? new Point()
            : e.GetPosition(_appDragSourceElement);
    }

    public void AppButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isInternalAppDrag
            || e.LeftButton != MouseButtonState.Pressed
            || _appDragEntry is null
            || _appDragSourceElement is null
            || _isInternalTileDrag)
        {
            return;
        }

        var delta = e.GetPosition(_window) - _appDragStart;
        if (delta.LengthSquared <= 9)
        {
            return;
        }

        ShowInternalDragPreview(
            _captureElement(_appDragSourceElement),
            _appDragSourceElement.ActualWidth,
            _appDragSourceElement.ActualHeight);
        _isInternalAppDrag = true;
        MoveAppDragPreview(e.GetPosition(_mainSurface));
        Mouse.Capture(_window, CaptureMode.SubTree);
        e.Handled = true;
    }

    private void MoveAppDragPreview(Point position)
    {
        MoveInternalDragPreview(position, _appDragAnchor);
    }

    private void EndInternalAppDrag(bool commit, Point position)
    {
        if (commit && _appDragEntry is { IsFolder: false } app)
        {
            var groupsPosition = _mainSurface.TranslatePoint(position, _tileGroupsControl);
            if (TryResolveTileAreaGroup(groupsPosition, out var target, out var groupControl))
            {
                _appController.AddAppTile(target, app, _tileGroupsControl.TranslatePoint(groupsPosition, groupControl),
                    _appDragAnchor);
            }
            else
            {
                var panePosition = _mainSurface.TranslatePoint(position, _tilePane);
                if (panePosition.X >= 0
                    && panePosition.Y >= 0
                    && panePosition.X < _tilePane.ActualWidth
                    && panePosition.Y < _tilePane.ActualHeight)
                {
                    var group = TileGroupManager.Add(_tileLayout);
                    _appController.AddAppTile(group, app, new Point(), _appDragAnchor);
                }
            }
        }

        Mouse.Capture(null);
        HideInternalDragPreview();
        _isInternalAppDrag = false;
        _appDragEntry = null;
        _appDragSourceElement = null;
        _setSuppressTileActivationUntil(Environment.TickCount64 + 300);
    }

    // =============================================
    // Tile drag
    // =============================================

    public void TileButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCompletingInternalTileDrag)
        {
            e.Handled = true;
            return;
        }

        _dragCompleted = false;
        _dragStart = e.GetPosition(_window);
        _dragAnchor = sender is Button button ? e.GetPosition(button) : new Point();
        _tileReflowStability.Reset();
        ResetFolderDropState();
        _dragSourceElement = sender as Button;
        _dragTile = _dragSourceElement?.Tag as TileItem;
        _dragSource = null;
        _dragSourceFolder = null;
        if (_dragTile is not null)
        {
            _findTileLocation(_dragTile, out _dragSource, out _dragSourceFolder);
        }
    }

    public void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isInternalAppDrag)
        {
            EndInternalAppDrag(commit: true, e.GetPosition(_mainSurface));
            e.Handled = true;
            return;
        }

        if (_isInternalTileDrag)
        {
            _internalDropIsValid = UpdateInternalTileDrag(e.GetPosition(_mainSurface), force: true);
            EndInternalTileDrag(commit: _internalDropIsValid);
            e.Handled = true;
            return;
        }

        if (_dragTransaction is null && !_dragCompleted)
        {
            ClearTileDragState();
        }
    }

    public void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isInternalAppDrag)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndInternalAppDrag(commit: false, e.GetPosition(_mainSurface));
                return;
            }

            MoveAppDragPreview(e.GetPosition(_mainSurface));
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

            var position = e.GetPosition(_mainSurface);
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

        var positionInWindow = e.GetPosition(_window);
        var delta = positionInWindow - _dragStart;
        if (delta.LengthSquared <= 9)
        {
            return;
        }

        BeginInternalTileDrag(e.GetPosition(_mainSurface));
        e.Handled = true;
    }

    private void BeginInternalTileDrag(Point position)
    {
        if (_dragTile is null || _dragSource is null || _dragSourceElement is null)
        {
            return;
        }

        var groupColumns = CurrentGroupColumnCount();
        if (EnsureGroupGridCoordinates())
        {
            _window.UpdateLayout();
            TileLayoutStore.Save(_tileLayout);
        }

        ShowInternalDragPreview(
            _captureElement(_dragSourceElement),
            _dragSourceElement.ActualWidth,
            _dragSourceElement.ActualHeight);
        MoveInternalDragPreview(position);
        _tileDragHitGeometry = new TileDragHitGeometry(CaptureTileAreaDropZones());
        _dragTransaction = new TileDragTransaction(
            _tileLayout,
            _dragSource,
            _dragSourceFolder,
            _dragTile,
            groupColumns);
        _dragTile.IsDragging = true;
        _dragCompleted = true;
        _isInternalTileDrag = true;
        _internalDropIsValid = UpdateInternalTileDrag(position, force: false);
        UpdateTileDragAutoScroll(position);
        Mouse.Capture(_window, CaptureMode.SubTree);
    }

    private void MoveInternalDragPreview(Point position)
    {
        MoveInternalDragPreview(position, _dragAnchor);
    }

    private void MoveInternalDragPreview(Point position, Point anchor)
    {
        _internalDragPreviewTransform.X = position.X - anchor.X;
        _internalDragPreviewTransform.Y = position.Y - anchor.Y;
    }

    private bool UpdateInternalTileDrag(Point position, bool force)
    {
        if (_dragTransaction is null || _dragTile is null)
        {
            return false;
        }

        var panePosition = _mainSurface.TranslatePoint(position, _tilePane);
        if (panePosition.X < 0
            || panePosition.Y < 0
            || panePosition.X >= _tilePane.ActualWidth
            || panePosition.Y >= _tilePane.ActualHeight)
        {
            ResetPendingTileDrop();
            return false;
        }

        var groupsPosition = _mainSurface.TranslatePoint(position, _tileGroupsControl);
        if (TryFindExpandedFolderAt(groupsPosition, out var folderControl, out var folder, out var group))
        {
            ResetFolderDropState();
            return PreviewFolderRegionDrop(
                group,
                folder,
                _tileGroupsControl.TranslatePoint(groupsPosition, folderControl),
                _dragTile,
                force);
        }

        var draggedBounds = new Rect(
            groupsPosition.X - _dragAnchor.X,
            groupsPosition.Y - _dragAnchor.Y,
            _dragTile.PixelWidth,
            _dragTile.PixelHeight);
        if (TryResolveTileAreaGroup(groupsPosition, out var target, out var groupControl, draggedBounds))
        {
            return PreviewTileDrop(
                target,
                _tileGroupsControl.TranslatePoint(groupsPosition, groupControl),
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
            _window.UpdateLayout();
            var movedGroups = AnimateGroupReorderFrom(previousGroupPositions);
            AnimateReorderFrom(previousPositions, movedGroups);
        }

        return true;
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
            TileLayoutStore.Save(_tileLayout);
        }

        transaction?.Dispose();
        if (!didCommit && rollbackPositions is not null)
        {
            _window.UpdateLayout();
            AnimateReorderFrom(rollbackPositions);
        }

        Mouse.Capture(null);
        _dragTransaction = null;
        _tileDragHitGeometry = null;
        _isInternalTileDrag = false;
        _internalDropIsValid = false;
        StopTileDragAutoScroll();
        _setSuppressTileActivationUntil(Environment.TickCount64 + 300);
        ResetPendingTileDrop();
        ClearTileDragState();

        if (tile is null
            || _internalDragPreview.Visibility != Visibility.Visible
            || !SystemParameters.ClientAreaAnimation)
        {
            CompleteInternalTileDragVisual(tile);
            DragCompleted?.Invoke(didCommit);
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

        DragCompleted?.Invoke(didCommit);
    }

    private void AnimateInternalDragPreviewHandoff(TileItem tile)
    {
        var duration = TimeSpan.FromMilliseconds(Win10ReorderMotion.DropHandoffDurationMilliseconds);
        var animation = Win10ReorderMotion.Create(_internalDragPreview.Opacity, 0, duration);
        animation.Completed += (_, _) => CompleteInternalTileDragVisual(tile);
        _internalDragPreview.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateInternalDragPreviewReturn(TileItem tile)
    {
        _window.UpdateLayout();
        if (FindReorderElement(tile) is not { } target)
        {
            CompleteInternalTileDragVisual(tile);
            return;
        }

        var targetPosition = target.TransformToAncestor(_mainSurface).Transform(new Point());
        var duration = TimeSpan.FromMilliseconds(Win10ReorderMotion.CancelReturnDurationMilliseconds);
        var x = Win10ReorderMotion.Create(_internalDragPreviewTransform.X, targetPosition.X, duration);
        var y = Win10ReorderMotion.Create(_internalDragPreviewTransform.Y, targetPosition.Y, duration);
        y.Completed += (_, _) => CompleteInternalTileDragVisual(tile);
        _internalDragPreviewTransform.BeginAnimation(
            TranslateTransform.XProperty,
            x,
            HandoffBehavior.SnapshotAndReplace);
        _internalDragPreviewTransform.BeginAnimation(
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
        _internalDragPreview.BeginAnimation(UIElement.OpacityProperty, null);
        _internalDragPreviewTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _internalDragPreviewTransform.BeginAnimation(TranslateTransform.YProperty, null);
        _internalDragPreview.Opacity = 0.96;
        _internalDragPreview.Source = source;
        _internalDragPreview.Width = width;
        _internalDragPreview.Height = height;
        _internalDragPreview.Visibility = Visibility.Visible;
    }

    private void HideInternalDragPreview()
    {
        _internalDragPreview.BeginAnimation(UIElement.OpacityProperty, null);
        _internalDragPreviewTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _internalDragPreviewTransform.BeginAnimation(TranslateTransform.YProperty, null);
        _internalDragPreview.Opacity = 0.96;
        _internalDragPreview.Source = null;
        _internalDragPreview.Visibility = Visibility.Collapsed;
    }

    // =============================================
    // Group drag
    // =============================================

    public void GroupHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
        _groupDragStart = e.GetPosition(_tileGroupsControl);
        header.CaptureMouse();
        e.Handled = true;
    }

    public void GroupHeader_PreviewMouseMove(object sender, MouseEventArgs e)
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

        var position = e.GetPosition(_tileGroupsControl);
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

    public void GroupHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
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

    public void GroupHeader_LostMouseCapture(object sender, MouseEventArgs e)
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
            TileLayoutStore.Save(_tileLayout);
        }

        var layoutOrigin = GetGroupLayoutPosition(container);
        var visibleOrigin = container.TransformToAncestor(_tileGroupsControl).Transform(new Point());
        _groupDragAnchor = _groupDragStart - visibleOrigin;
        _groupDragContainer = container;
        _groupDragTransform = new TranslateTransform(
            visibleOrigin.X - layoutOrigin.X,
            visibleOrigin.Y - layoutOrigin.Y);
        var existingTargets = _tileLayout.Groups
            .Select(group => (group, container: GetGroupContainer(group)))
            .Where(item => item.container is not null)
            .Select(item => new TileGroupDropTarget(
                item.group.GroupColumn,
                item.group.GroupRow,
                new Rect(
                    GetGroupLayoutPosition(item.container!),
                    new Size(item.container!.ActualWidth, item.container.ActualHeight)),
                ColumnSpan: item.group.WidthUnits))
            .ToArray();
        _groupDragTargets = TileGroupDropResolver.IncludeEmptyColumns(
            existingTargets,
            groupColumns,
            _groupDragGroup.WidthUnits);
        _groupDragTransaction = new TileGroupDragTransaction(_tileLayout, _groupDragGroup, groupColumns);
        _isInternalGroupDrag = true;
        _groupDragHeader.SetDragging(true);
        _groupDragContainer.RenderTransform = _groupDragTransform;
        _groupDragContainer.Opacity = 0.96;
        Panel.SetZIndex(_groupDragContainer, 1000);
        return true;
    }

    private void UpdateGroupDrag(Point position)
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
            ? new Point()
            : container.TransformToAncestor(_tileGroupsControl).Transform(new Point());
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
            Panel.SetZIndex(container, 0);
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
            TileLayoutStore.Save(_tileLayout);
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

    internal Dictionary<TileGroup, Point> CaptureGroupReorderPositions()
    {
        return _tileLayout.Groups
            .Where(group => !ReferenceEquals(group, _groupDragGroup))
            .Select(group => (group, container: GetGroupContainer(group)))
            .Where(item => item.container is not null)
            .ToDictionary(
                item => item.group,
                item => item.container!.TransformToAncestor(_tileGroupsControl).Transform(new Point()));
    }

    internal HashSet<TileGroup> AnimateGroupReorderFrom(
        IReadOnlyDictionary<TileGroup, Point> previousPositions)
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
        return _tileGroupsControl.ItemContainerGenerator.ContainerFromItem(group) as FrameworkElement;
    }

    private Point GetGroupLayoutPosition(FrameworkElement container)
    {
        var offset = VisualTreeHelper.GetOffset(container);
        return VisualTreeHelper.GetParent(container) is Visual parent
            ? parent.TransformToAncestor(_tileGroupsControl).Transform(new Point()) + offset
            : new Point(offset.X, offset.Y);
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

    private void ClearTileDragState()
    {
        _dragTile = null;
        _dragSource = null;
        _dragSourceFolder = null;
        _dragSourceElement = null;
    }

    // =============================================
    // Auto-scroll
    // =============================================

    private void UpdateTileDragAutoScroll(Point position)
    {
        _lastInternalTileDragPosition = position;
        var pointer = _mainSurface.TranslatePoint(position, _tileScrollViewer);
        _tileDragAutoScrollVelocity = pointer.X >= 0 && pointer.X < _tileScrollViewer.ActualWidth
            ? TileDragAutoScroll.GetVelocity(
                pointer.Y,
                _tileScrollViewer.ActualHeight,
                _tileScrollViewer.VerticalOffset,
                _tileScrollViewer.ScrollableHeight)
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
            _tileScrollViewer.VerticalOffset,
            _tileScrollViewer.ScrollableHeight,
            _tileDragAutoScrollVelocity,
            elapsedSeconds);
        if (Math.Abs(nextOffset - _tileScrollViewer.VerticalOffset) < 0.1)
        {
            UpdateTileDragAutoScroll(_lastInternalTileDragPosition);
            return;
        }

        _tileScrollViewer.ScrollToVerticalOffset(nextOffset);
        _tileScrollViewer.UpdateLayout();
        _internalDropIsValid = UpdateInternalTileDrag(_lastInternalTileDragPosition, force: false);
        UpdateTileDragAutoScroll(_lastInternalTileDragPosition);
    }

    // =============================================
    // Drop zone handlers
    // =============================================

    public void TileGroup_DragOver(object sender, DragEventArgs e)
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

    public void TileGroup_Drop(object sender, DragEventArgs e)
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
                TileLayoutStore.Save(_tileLayout);
                e.Effects = DragDropEffects.Move;
            }
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddDroppedTiles(target, paths, position);
        }

        e.Handled = true;
    }

    public void FolderRegion_DragOver(object sender, DragEventArgs e)
    {
        if (sender is ItemsControl { Tag: TileItem folder } itemsControl
            && e.Data.GetData(typeof(TileItem)) is TileItem tile
            && _dragTransaction is not null
            && _findTileLocation(folder, out var group, out _))
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

    public void FolderRegion_Drop(object sender, DragEventArgs e)
    {
        if (sender is ItemsControl { Tag: TileItem folder } itemsControl
            && e.Data.GetData(typeof(TileItem)) is TileItem tile
            && _dragTransaction is not null
            && _findTileLocation(folder, out var group, out _)
            && PreviewFolderRegionDrop(group, folder, e.GetPosition(itemsControl), tile, force: true))
        {
            _dragTransaction.Commit();
            TileLayoutStore.Save(_tileLayout);
            e.Effects = DragDropEffects.Move;
        }

        e.Handled = true;
    }

    public void TileArea_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TileItem)) is TileItem tile && _dragTransaction is not null)
        {
            if (TryResolveTileAreaGroup(e.GetPosition(_tileGroupsControl), out var target, out var groupControl))
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
                    _window.UpdateLayout();
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

    public void TileArea_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TileItem)) is TileItem tile && _dragTransaction is not null)
        {
            if (TryResolveTileAreaGroup(e.GetPosition(_tileGroupsControl), out var target, out var groupControl))
            {
                var position = e.GetPosition(groupControl);
                if (PreviewTileDrop(target, position, tile, force: true))
                {
                    _dragTransaction.Commit();
                    TileLayoutStore.Save(_tileLayout);
                    e.Effects = DragDropEffects.Move;
                }
            }
            else if (_dragTransaction.PreviewTarget is not null)
            {
                _dragTransaction.Commit();
                TileLayoutStore.Save(_tileLayout);
                e.Effects = DragDropEffects.Move;
            }
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var group = TileGroupManager.Add(_tileLayout);
            if (AddDroppedTiles(group, paths, new Point()))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                TileGroupManager.Remove(_tileLayout, group);
                e.Effects = DragDropEffects.None;
            }
        }

        e.Handled = true;
    }

    // =============================================
    // Drop zone helpers
    // =============================================

    private bool TryResolveTileAreaGroup(
        Point pointer,
        out TileGroup target,
        out ItemsControl groupControl,
        Rect? draggedBounds = null)
    {
        var controls = FindVisualDescendants<ItemsControl>(_tileGroupsControl)
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
                var origin = control.TransformToAncestor(_tileGroupsControl).Transform(new Point());
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
        return FindVisualDescendants<ItemsControl>(_tileGroupsControl)
            .Where(control => control.Tag is TileGroup)
            .Select(control =>
            {
                var group = (TileGroup)control.Tag;
                var origin = control.TransformToAncestor(_tileGroupsControl).Transform(new Point());
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

    private TileNewGroupDropTarget ResolveNewTileGroupTarget(Rect draggedBounds)
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

    private bool TryFindExpandedFolderAt(
        Point pointer,
        out ItemsControl control,
        out TileItem folder,
        out TileGroup group)
    {
        foreach (var candidate in FindVisualDescendants<ItemsControl>(_tileGroupsControl)
                     .Where(item => item.Tag is TileItem { IsTileFolder: true, IsFolderExpanded: true }))
        {
            var origin = candidate.TransformToAncestor(_tileGroupsControl).Transform(new Point());
            if (pointer.X < origin.X
                || pointer.X >= origin.X + candidate.ActualWidth
                || pointer.Y < origin.Y
                || pointer.Y >= origin.Y + candidate.ActualHeight)
            {
                continue;
            }

            folder = (TileItem)candidate.Tag;
            if (_findTileLocation(folder, out group, out _))
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

    // =============================================
    // Reorder and layout helpers
    // =============================================

    internal int CurrentGroupColumnCount()
    {
        var availableWidth = _tileScrollViewer.ViewportWidth;
        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            availableWidth = _tilePane.ActualWidth - _tileScrollViewer.Margin.Left;
        }

        if (!double.IsFinite(availableWidth) || availableWidth <= 0)
        {
            availableWidth = Math.Max(Win10TileMetrics.GroupWidth, _window.Width - 340);
        }

        var widthColumns = Win10GroupWrapPanel.ColumnsForWidth(availableWidth);
        var visualColumns = _tileLayout.Groups
            .Select(group => new { Group = group, Container = GetGroupContainer(group) })
            .Where(item => item.Container is not null)
            .Select(item => (int)Math.Round(
                                GetGroupLayoutPosition(item.Container!).X / TileWorkspaceMetrics.ColumnPitch)
                            + item.Group.WidthUnits)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(1, Math.Max(widthColumns, visualColumns));
    }

    internal void RefreshGroupPanelLayout()
    {
        var panel = FindVisualDescendants<Win10GroupWrapPanel>(_tileGroupsControl).FirstOrDefault();
        panel?.InvalidateMeasure();
        panel?.InvalidateArrange();
        _tileGroupsControl.InvalidateMeasure();
        _tileGroupsControl.InvalidateArrange();
        _tileGroupsControl.UpdateLayout();
    }

    internal bool EnsureGroupGridCoordinates() =>
        Win10GroupGridLayout.EnsureCoordinates(_tileLayout, CurrentGroupColumnCount());

    internal bool EnsureTileScrollBarClearance()
    {
        if (_tileScrollViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
        {
            return false;
        }

        var scrollBar = FindVisualDescendants<ScrollBar>(_tileScrollViewer)
            .FirstOrDefault(candidate => candidate.Orientation == System.Windows.Controls.Orientation.Vertical);
        if (scrollBar is null)
        {
            return false;
        }

        var footprint = scrollBar.ActualWidth + scrollBar.Margin.Left + scrollBar.Margin.Right;
        var viewportWidth = _tileScrollViewer.ViewportWidth;
        var columns = Win10GroupWrapPanel.ColumnsForWidth(viewportWidth);
        var deficit = Win10GroupWrapPanel.OverlayClearanceDeficit(viewportWidth, columns, footprint);
        if (deficit < 0.1)
        {
            return false;
        }

        _window.Width += deficit;
        _window.UpdateLayout();
        return true;
    }

    // =============================================
    // Folder hover and activation
    // =============================================

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

    // =============================================
    // Drop preview and reflow
    // =============================================

    private bool PreviewFolderRegionDrop(
        TileGroup target,
        TileItem folder,
        Point position,
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
            _window.UpdateLayout();
            AnimateReorderFrom(previousPositions);
        }

        return previewed;
    }

    private bool PreviewTileDrop(
        TileGroup target,
        Point position,
        TileItem tile,
        bool force)
    {
        var logicalPosition = new Point(
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
            _window.UpdateLayout();
            AnimateReorderFrom(previousPositions);
        }

        return previewed;
    }

    private void RestartTileReflowTimer()
    {
        _tileReflowTimer.Stop();
        _tileReflowTimer.Start();
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

    // =============================================
    // External drop (file drop)
    // =============================================

    private bool AddDroppedTiles(TileGroup target, IEnumerable<string> paths, Point position)
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
            TileLayoutStore.Save(_tileLayout);
        }

        return added;
    }

    // =============================================
    // Reorder capture and animation
    // =============================================

    internal Dictionary<TileItem, Point> CaptureReorderPositions()
    {
        var positions = new Dictionary<TileItem, Point>();
        foreach (var group in _tileLayout.Groups)
        {
            var groupControl = FindVisualDescendants<ItemsControl>(_tileGroupsControl)
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

                var folderControl = FindVisualDescendants<ItemsControl>(_tileGroupsControl)
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
        IDictionary<TileItem, Point> positions)
    {
        if (candidate is FrameworkElement { IsVisible: true } element
            && element.IsDescendantOf(_tileGroupsControl))
        {
            positions[tile] = element.TransformToAncestor(_tileGroupsControl).Transform(new Point());
        }
    }

    internal void AnimateReorderFrom(
        IReadOnlyDictionary<TileItem, Point> previousPositions,
        IReadOnlySet<TileGroup>? groupsAnimatedAsContainers = null)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        foreach (var (tile, previous) in previousPositions)
        {
            if (groupsAnimatedAsContainers is not null
                && _findTileLocation(tile, out var group, out _)
                && groupsAnimatedAsContainers.Contains(group))
            {
                continue;
            }

            if (FindReorderElement(tile) is not { } element)
            {
                continue;
            }

            var current = element.TransformToAncestor(_tileGroupsControl).Transform(new Point());
            var activeTranslation = element.RenderTransform is TranslateTransform transform
                ? new Vector(transform.X, transform.Y)
                : new Vector();
            var delta = Win10ReorderMotion.ResolveRetargetDelta(previous, current, activeTranslation);
            Win10ReorderMotion.AnimateFrom(element, delta);
        }
    }

    private FrameworkElement? FindReorderElement(TileItem tile)
    {
        if (!_findTileLocation(tile, out var group, out var folder))
        {
            return null;
        }

        var itemsControl = folder is null
            ? FindVisualDescendants<ItemsControl>(_tileGroupsControl)
                .FirstOrDefault(control => ReferenceEquals(control.Tag, group))
            : FindVisualDescendants<ItemsControl>(_tileGroupsControl)
                .FirstOrDefault(control => ReferenceEquals(control.Tag, folder));
        return itemsControl?.ItemContainerGenerator.ContainerFromItem(tile) as FrameworkElement;
    }

    // =============================================
    // Context menu
    // =============================================

    public void OpenAppFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AppEntry app })
        {
            if (ApplicationPaneController.IsMissingFileApplication(app))
            {
                _appController.RemoveApplicationFromList(LaunchTargetIdentity.GetKey(app.LaunchTarget));
                DiagnosticLog.Write($"Removed missing application entry: {app.LaunchTarget}");
                return;
            }

            AppLauncher.OpenFileLocation(app);
        }
    }

    // =============================================
    // DEBUG trace
    // =============================================

#if DEBUG
    private void TraceTileDropGeometry(
        TileGroup target,
        Point logicalPosition,
        Point displayPosition,
        TileItem moving,
        int column,
        int row,
        TileItem? folderTarget)
    {
        var draggedBounds = new Rect(
            logicalPosition.X - _dragAnchor.X,
            logicalPosition.Y - _dragAnchor.Y,
            moving.PixelWidth,
            moving.PixelHeight);
        var draggedCenter = new Point(
            draggedBounds.Left + draggedBounds.Width / 2,
            draggedBounds.Top + draggedBounds.Height / 2);
        var tiles = target.Tiles
            .Where(tile => !ReferenceEquals(tile, moving))
            .Select(tile =>
                (Tile: tile,
                    Bounds: new Rect(tile.Left, tile.Top, tile.PixelWidth, tile.PixelHeight)))
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
        (TileItem? Tile, Rect Bounds) candidate,
        Rect draggedBounds,
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

    private static string FormatBounds(Rect bounds) =>
        $"({bounds.Left:F1},{bounds.Top:F1},{bounds.Right:F1},{bounds.Bottom:F1})";

    private enum Axis
    {
        None,
        Horizontal,
        Vertical,
    }
#endif

    // =============================================
    // Visual tree helpers
    // =============================================

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