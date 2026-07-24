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
using System.Windows.Controls;
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

namespace TileStart.Host.Controllers;

internal sealed class TileWorkspaceController
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    private int _appFolderAnimationGeneration;
    private int _tileFolderAnimationGeneration;
    private bool _isAppFolderAnimating;
    private bool _isTileFolderAnimating;

    private readonly Window _window;
    private readonly TileLayout _tileLayout;
    private readonly TileDragCoordinator _tileDragCoordinator;
    private readonly ApplicationPaneController _appController;
    private readonly NavigationController _navigationController;
    private readonly Grid _navigationPane;
    private readonly ItemsControl _tileGroupsControl;
    private readonly ItemsControl _appsList;
    private readonly Action<bool> _dismissWindow;
    private readonly Action<string> _tryDismissAfterForegroundChange;
    private readonly Func<int> _getOpenContextMenuCount;
    private readonly Action<int> _setOpenContextMenuCount;
    private readonly Func<long> _getSuppressTileActivationUntil;

    public TileWorkspaceController(
        Window window,
        TileLayout tileLayout,
        TileDragCoordinator tileDragCoordinator,
        ApplicationPaneController appController,
        NavigationController navigationController,
        Grid navigationPane,
        ItemsControl tileGroupsControl,
        ItemsControl appsList,
        Action<bool> dismissWindow,
        Action<string> tryDismissAfterForegroundChange,
        Func<int> getOpenContextMenuCount,
        Action<int> setOpenContextMenuCount,
        Func<long> getSuppressTileActivationUntil)
    {
        _window = window;
        _tileLayout = tileLayout;
        _tileDragCoordinator = tileDragCoordinator;
        _appController = appController;
        _navigationController = navigationController;
        _navigationPane = navigationPane;
        _tileGroupsControl = tileGroupsControl;
        _appsList = appsList;
        _dismissWindow = dismissWindow;
        _tryDismissAfterForegroundChange = tryDismissAfterForegroundChange;
        _getOpenContextMenuCount = getOpenContextMenuCount;
        _setOpenContextMenuCount = setOpenContextMenuCount;
        _getSuppressTileActivationUntil = getSuppressTileActivationUntil;
    }

    public TileLayout GetTileLayout() => _tileLayout;

    // ── Context menu ──────────────────────────────────────────────

    public void StartContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _setOpenContextMenuCount(_getOpenContextMenuCount() + 1);
        if (sender is not ContextMenu menu)
        {
            return;
        }

        if (GetContextMenuPopupBorder(menu) is { } border)
        {
            var opensUpward = ContextMenuOpensUpward(menu, border);
            AnimateMenuPopupBorder(
                border,
                Win10MenuPopupMotion.TopLevelClosedRatio,
                opensUpward,
                opensUpward ? null : PointerOriginY(border),
                useSubmenuDirection: true);
        }

        if (menu.PlacementTarget is Button { Tag: TileItem tile })
        {
            foreach (var item in EnumerateMenuItems(menu))
            {
                if (item.Tag as string == "OpenFileLocation")
                {
                    item.Visibility = AppLauncher.CanOpenFileLocation(tile, _appController.LaunchableApps)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (item.Tag as string == "DissolveFolder")
                {
                    item.Visibility = tile.IsTileFolder ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (item.Tag as string == "Uninstall")
                {
                    item.Visibility = AppUninstaller.CanUninstall(tile, _appController.LaunchableApps)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (item.Tag as string == "PinTaskbar")
                {
                    item.Visibility = TaskbarPinner.CanPin(tile)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (item.IsCheckable)
                {
                    item.IsChecked = TileContextActions.IsSelectedSize(tile.Size, item.Tag as string);
                }
            }
        }
        else if (menu.PlacementTarget is Button { Tag: AppEntry app })
        {
            foreach (var item in EnumerateMenuItems(menu))
            {
                if (item.Tag as string == "UnpinStart")
                {
                    item.Visibility = IsPinnedToStart(app)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (item.Tag as string == "RemoveCustomApp")
                {
                    item.Visibility = app.IsCustom ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    internal static IEnumerable<MenuItem> EnumerateMenuItems(ItemsControl owner)
    {
        foreach (var item in owner.Items.OfType<MenuItem>())
        {
            yield return item;
            foreach (var child in EnumerateMenuItems(item))
            {
                yield return child;
            }
        }
    }

    public void SubmenuPopup_Opened(object? sender, EventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation
            || sender is not System.Windows.Controls.Primitives.Popup
            {
                Child: System.Windows.Controls.Border border,
            } popup)
        {
            return;
        }

        border.UpdateLayout();
        if (border.ActualWidth <= 0 || border.ActualHeight <= 0)
        {
            return;
        }

        var submenuOpensUpward = false;
        double? pointerOriginY = null;
        if (popup.PlacementTarget is FrameworkElement placementTarget)
        {
            try
            {
                submenuOpensUpward = border.PointToScreen(new System.Windows.Point()).Y
                                     < placementTarget.PointToScreen(new System.Windows.Point()).Y - 0.5;
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (!submenuOpensUpward)
        {
            pointerOriginY = PointerOriginY(border);
        }

        AnimateMenuPopupBorder(
            border,
            Win10MenuPopupMotion.SubmenuClosedRatio,
            submenuOpensUpward,
            pointerOriginY,
            useSubmenuDirection: true);
    }

    private static void AnimateMenuPopupBorder(
        System.Windows.Controls.Border border,
        double closedRatio,
        bool popupOpensUpward,
        double? pointerOriginY = null,
        bool useSubmenuDirection = false)
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

        var fullRect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
        var clip = new RectangleGeometry(fullRect);
        border.Clip = clip;
        var animation = Win10MenuPopupMotion.CreateOpenAnimation(
            border.ActualWidth,
            border.ActualHeight,
            closedRatio,
            popupOpensUpward,
            pointerOriginY,
            useSubmenuDirection);
        animation.Completed += (_, _) =>
        {
            if (ReferenceEquals(border.Clip, clip))
            {
                border.ClearValue(UIElement.ClipProperty);
            }
        };
        clip.BeginAnimation(
            RectangleGeometry.RectProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private static System.Windows.Controls.Border? GetContextMenuPopupBorder(ContextMenu menu) =>
        menu.Template.FindName("ContextMenuPopupBorder", menu) as System.Windows.Controls.Border;

    private static double? PointerOriginY(FrameworkElement popup)
    {
        if (!GetCursorPos(out var cursor))
        {
            return null;
        }

        try
        {
            var localPointer = popup.PointFromScreen(new System.Windows.Point(cursor.X, cursor.Y));
            return localPointer.Y >= 0 && localPointer.Y <= popup.ActualHeight
                ? localPointer.Y
                : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool ContextMenuOpensUpward(
        ContextMenu menu,
        System.Windows.Controls.Border border)
    {
        try
        {
            var menuTop = border.PointToScreen(new System.Windows.Point()).Y;
            if (menu.Placement == System.Windows.Controls.Primitives.PlacementMode.Right
                && menu.PlacementTarget is FrameworkElement placementTarget)
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

    public void SubmenuPopup_Closed(object? sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Popup
            {
                Child: System.Windows.Controls.Border border,
            })
        {
            border.ClearValue(UIElement.ClipProperty);
        }
    }

    public void StartContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _setOpenContextMenuCount(Math.Max(0, _getOpenContextMenuCount() - 1));
        if (sender is ContextMenu menu && GetContextMenuPopupBorder(menu) is { } border)
        {
            border.ClearValue(UIElement.ClipProperty);
        }

        if (!_navigationController.IsNavigationPinnedOpen && !_navigationPane.IsMouseOver)
        {
            _navigationController.SetNavigationExpanded(false);
        }

        _window.Dispatcher.BeginInvoke(
            () => _tryDismissAfterForegroundChange("context-menu-closed"),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // ── Pin / unpin ───────────────────────────────────────────────

    public void PinAppToStart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem
            || ItemsControl.ItemsControlFromItemContainer(menuItem) is not ContextMenu
            {
                PlacementTarget: Button { Tag: AppEntry app },
            }
            || app.IsFolder)
        {
            return;
        }

        var tile = _appController.CreateAppTile(app);
        PinTileToStart(tile);
    }

    public void UnpinAppFromStart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item
            || ItemsControl.ItemsControlFromItemContainer(item) is not ContextMenu
            {
                PlacementTarget: Button { Tag: AppEntry app },
            })
        {
            return;
        }

        var identity = LaunchTargetIdentity.GetKey(app.LaunchTarget);
        var tiles = _tileLayout.Groups
            .SelectMany(group => group.Tiles)
            .Where(tile => LaunchTargetIdentity.GetKey(tile.LaunchTarget) == identity)
            .ToArray();
        var changed = false;
        foreach (var tile in tiles)
        {
            changed |= TileContextActions.Unpin(_tileLayout, tile);
        }

        if (changed)
        {
            TileLayoutStore.Save(_tileLayout);
        }
    }

    private bool IsPinnedToStart(AppEntry app)
    {
        var identity = LaunchTargetIdentity.GetKey(app.LaunchTarget);
        return _tileLayout.Groups
            .SelectMany(group => group.Tiles)
            .Any(tile => LaunchTargetIdentity.GetKey(tile.LaunchTarget) == identity);
    }

    public void RemoveCustomApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item
            || ItemsControl.ItemsControlFromItemContainer(item) is not ContextMenu
            {
                PlacementTarget: Button { Tag: AppEntry app },
            }
            || !app.IsCustom)
        {
            return;
        }

        if (!CustomAppStore.Remove(app.LaunchTarget))
        {
            return;
        }

        var identity = LaunchTargetIdentity.GetKey(app.LaunchTarget);
        _appController.RemoveApplicationFromList(identity);
        _appController.ShowIfHidden();
    }

    public bool PinTileToStart(TileItem tile)
    {
        var placement = Win10GroupLayout.FindPinPlacement(_tileLayout.Groups, tile)
                        ?? new Win10PinPlacement(
                            TileGroupManager.Add(_tileLayout, _tileDragCoordinator.CurrentGroupColumnCount()),
                            0,
                            0);
        if (Win10GroupLayout.AddToFreeCell(placement.Group, tile, placement.Column, placement.Row))
        {
            TileLayoutStore.Save(_tileLayout);
            return true;
        }

        return false;
    }

    // ── Tile actions ──────────────────────────────────────────────

    public void TileButton_Click(object sender, RoutedEventArgs e)
    {
        var suppress = _tileDragCoordinator.DragCompletedFlag ||
                       Environment.TickCount64 <= _getSuppressTileActivationUntil();
        if (suppress)
        {
            _tileDragCoordinator.ResetDragCompletedFlag();
            return;
        }

        if (sender is not Button { Tag: TileItem tile })
        {
            return;
        }

        if (tile.IsTileFolder)
        {
            var group = _tileLayout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(tile));
            if (group is not null)
            {
                _ = ToggleTileFolderAsync(group, tile);
            }

            return;
        }

        if (AppLauncher.Launch(tile))
        {
            _dismissWindow(true);
        }
    }

    public void TileSettings_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is null)
        {
            return;
        }

        var group = _tileLayout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(tile));
        if (group is null)
        {
            return;
        }

        var defaultVisual = new TileItem
        {
            LaunchTarget = tile.LaunchTarget,
            TargetType = tile.TargetType,
            Size = tile.Size,
            IconSize = tile.IconSize,
            IconPosition = tile.IconPosition,
        };
        ApplicationPaneController.RestoreTileIcon(defaultVisual, _appController.LaunchableApps);
        var dialog = new TileSettingsWindow(
            tile,
            defaultIcon: defaultVisual.Icon,
            defaultUsesFullTileLogo: defaultVisual.UsesFullTileLogo);
        if (ShowTileSettingsDialog(dialog) != true)
        {
            return;
        }

        if (dialog.ShouldUnpin)
        {
            TileContextActions.Unpin(_tileLayout, tile);
            TileLayoutStore.Save(_tileLayout);
            return;
        }

        ApplyTileSettings(tile, dialog);
        Win10GroupLayout.Normalize(group);
        TileLayoutStore.Save(_tileLayout);
    }

    public void UnpinTile_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null && TileContextActions.Unpin(_tileLayout, tile))
        {
            TileLayoutStore.Save(_tileLayout);
        }
    }

    public void DissolveFolder_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is null)
        {
            return;
        }

        var previousPositions = _tileDragCoordinator.CaptureReorderPositions();
        if (!TileContextActions.DissolveFolder(_tileLayout, tile))
        {
            return;
        }

        _window.UpdateLayout();
        _tileDragCoordinator.AnimateReorderFrom(previousPositions);
        TileLayoutStore.Save(_tileLayout);
    }

    public void ResizeTile_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null
            && sender is MenuItem { Tag: string sizeName }
            && Enum.TryParse<TileSize>(sizeName, out var size)
            && TileContextActions.Resize(_tileLayout, tile, size))
        {
            ApplicationPaneController.RestoreTileIcon(tile, _appController.LaunchableApps);
            TileLayoutStore.Save(_tileLayout);
        }
    }

    public void OpenTileFileLocation_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null)
        {
            AppLauncher.OpenFileLocation(tile, _appController.LaunchableApps);
        }
    }

    public void UninstallApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AppEntry app } && AppUninstaller.Open(app))
        {
            _dismissWindow(true);
        }
    }

    public void UninstallTile_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null && AppUninstaller.Open(tile, _appController.LaunchableApps))
        {
            _dismissWindow(true);
        }
    }

    public async void PinAppToTaskbar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AppEntry app })
        {
            await RequestTaskbarPinAsync(app);
        }
    }

    public async void PinTileToTaskbar_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is null)
        {
            return;
        }

        if (FindApp(tile) is { } app)
        {
            await RequestTaskbarPinAsync(app);
            return;
        }

        if (await TaskbarPinner.RequestPinAsync(tile))
        {
            _dismissWindow(true);
            return;
        }

        ShowTaskbarPinFailed();
    }

    private async Task RequestTaskbarPinAsync(AppEntry app)
    {
        if (await TaskbarPinner.RequestPinAsync(app))
        {
            _dismissWindow(true);
            return;
        }

        ShowTaskbarPinFailed();
    }

    private void ShowTaskbarPinFailed() => System.Windows.MessageBox.Show(_window,
        "Windows 没有允许固定该应用，或该应用已经固定到任务栏。",
        "固定到任务栏",
        MessageBoxButton.OK,
        MessageBoxImage.Information);

    private AppEntry? FindApp(TileItem tile) => _appController.LaunchableApps.FirstOrDefault(candidate =>
        candidate.LaunchTarget.Equals(tile.LaunchTarget, StringComparison.OrdinalIgnoreCase));

    public void RunTileAsAdministrator_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null && AppLauncher.LaunchAsAdministrator(tile))
        {
            _dismissWindow(true);
        }
    }

    public static TileItem? GetContextTile(object sender)
    {
        if (sender is not MenuItem item)
        {
            return null;
        }

        ItemsControl? owner = ItemsControl.ItemsControlFromItemContainer(item);
        while (owner is MenuItem parent)
        {
            owner = ItemsControl.ItemsControlFromItemContainer(parent);
        }

        return owner is ContextMenu { PlacementTarget: Button { Tag: TileItem tile } }
            ? tile
            : null;
    }

    public void AddCommandTile_Click(object sender, RoutedEventArgs e)
    {
        var tile = new TileItem
        {
            Name = "新磁贴",
            TargetType = TileTargetType.Command,
            Size = TileSize.Medium,
        };
        var dialog = new TileSettingsWindow(tile, true);
        if (ShowTileSettingsDialog(dialog) != true)
        {
            return;
        }

        ApplyTileSettings(tile, dialog);
        var placement = Win10GroupLayout.FindPinPlacement(_tileLayout.Groups, tile)
                        ?? new Win10PinPlacement(
                            TileGroupManager.Add(_tileLayout, _tileDragCoordinator.CurrentGroupColumnCount()),
                            0,
                            0);
        if (Win10GroupLayout.AddToFreeCell(placement.Group, tile, placement.Column, placement.Row))
        {
            TileLayoutStore.Save(_tileLayout);
        }
    }

    // ── Group actions ─────────────────────────────────────────────

    public void GroupHeader_NameCommitted(object sender, EventArgs e)
    {
        TileLayoutStore.Save(_tileLayout);
    }

    public void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        var group = GetContextGroup(sender);
        if (group is null)
        {
            return;
        }

        if (group.Tiles.Count > 0
            && System.Windows.MessageBox.Show(_window,
                "删除该组会同时取消固定其中的全部磁贴。是否继续？",
                "删除组",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (TileGroupManager.Remove(_tileLayout, group))
        {
            TileLayoutStore.Save(_tileLayout);
        }
    }

    public void GroupSettings_Click(object sender, RoutedEventArgs e)
    {
        var group = GetContextGroup(sender);
        if (group is null)
        {
            return;
        }

        GroupSettingsWindow dialog;
        try
        {
            dialog = new GroupSettingsWindow(group, _appController.LaunchableApps);
            if (ShowGroupSettingsDialog(dialog) != true)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Unable to open group settings: {exception}");
            System.Windows.MessageBox.Show(
                _window,
                "无法打开组设置，错误已写入 TileStart 日志。",
                "TileStart",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var previousName = group.Name;
        var previousWidthUnits = group.WidthUnits;
        var previousHeightUnits = group.HeightUnits;
        var previousTiles = group.Tiles.ToArray();
        var selectedTiles = dialog.SelectedOptions
            .Select(option => option.ExistingTile ?? _appController.CreateAppTile(option.App!))
            .ToArray();

        group.Name = dialog.GroupName;
        group.WidthUnits = dialog.WidthUnits;
        group.HeightUnits = dialog.HeightUnits;
        group.Tiles.Clear();
        foreach (var tile in selectedTiles)
        {
            group.Tiles.Add(tile);
        }

        if (!Win10GroupLayout.Normalize(group))
        {
            group.Name = previousName;
            group.WidthUnits = previousWidthUnits;
            group.HeightUnits = previousHeightUnits;
            group.Tiles.Clear();
            foreach (var tile in previousTiles)
            {
                group.Tiles.Add(tile);
            }

            Win10GroupLayout.Normalize(group);
            System.Windows.MessageBox.Show(
                _window,
                "组内容无法应用到所选尺寸，原布局已恢复。",
                "TileStart",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _tileDragCoordinator.EnsureGroupGridCoordinates();
        _tileDragCoordinator.RefreshGroupPanelLayout();
        TileLayoutStore.Save(_tileLayout);
    }

    public static TileGroup? GetContextGroup(object sender)
    {
        return sender is MenuItem menuItem
               && ItemsControl.ItemsControlFromItemContainer(menuItem) is ContextMenu
               {
                   PlacementTarget: FrameworkElement { DataContext: TileGroup group },
               }
            ? group
            : null;
    }

    private void ApplyTileSettings(TileItem tile, TileSettingsWindow dialog)
    {
        tile.Name = dialog.TileName;
        tile.Subtitle = dialog.Subtitle;
        tile.LaunchTarget = dialog.LaunchTarget;
        tile.Arguments = dialog.Arguments;
        tile.WorkingDirectory = dialog.WorkingDirectory;
        tile.IconPath = dialog.IconPath;
        tile.IconSourceKind = dialog.IconSourceKind;
        tile.IconSourceValue = dialog.IconSourceValue;
        tile.BackgroundImagePath = dialog.BackgroundImagePath;
        tile.BackgroundImageScale = dialog.BackgroundImageScale;
        tile.BackgroundColor = dialog.BackgroundColor;
        tile.ForegroundColor = dialog.ForegroundColor;
        tile.ShowTitle = dialog.ShowTitle;
        tile.IconSize = dialog.IconSize;
        tile.IconPosition = dialog.IconPosition;
        tile.RunAsAdministrator = dialog.RunAsAdministrator;
        tile.Size = dialog.TileSize;
        ApplicationPaneController.RestoreTileIcon(tile, _appController.LaunchableApps);
        tile.BackgroundImage = ShellIconLoader.LoadImage(tile.BackgroundImagePath);
    }

    private bool? ShowTileSettingsDialog(TileSettingsWindow dialog)
    {
        var wasTopmost = _window.Topmost;
        _window.Topmost = false;
        dialog.Owner = _window;
        try
        {
            return dialog.ShowDialog();
        }
        finally
        {
            _window.Topmost = wasTopmost;
        }
    }

    private bool? ShowGroupSettingsDialog(GroupSettingsWindow dialog)
    {
        var wasTopmost = _window.Topmost;
        _window.Topmost = false;
        dialog.Owner = _window;
        try
        {
            return dialog.ShowDialog();
        }
        finally
        {
            _window.Topmost = wasTopmost;
        }
    }

    // ── App folder animations ─────────────────────────────────────

    public async Task ToggleAppFolderAsync(AppEntry folder)
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
                _window.UpdateLayout();
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
            _window.UpdateLayout();
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
        foreach (var button in FindVisualDescendants<Button>(_appsList))
        {
            if (button.Tag is not AppEntry app
                || button.Parent is not FrameworkElement root
                || !root.IsVisible
                || !root.IsDescendantOf(_appsList))
            {
                continue;
            }

            positions[app] = root.TransformToAncestor(_appsList).Transform(new System.Windows.Point());
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

            var current = root.TransformToAncestor(_appsList).Transform(new System.Windows.Point());
            var delta = previous.Y - current.Y;
            AnimateTranslateY(
                root,
                delta,
                Win10FolderMotion.AppReflowDurationMilliseconds,
                Win10FolderMotion.StandardSpline);
        }
    }

    private FrameworkElement? FindAppEntryRoot(AppEntry app) =>
        FindVisualDescendants<Button>(_appsList)
            .FirstOrDefault(button => ReferenceEquals(button.Tag, app))?.Parent as FrameworkElement;

    private ItemsControl? FindAppFolderControl(AppEntry folder) =>
        FindVisualDescendants<ItemsControl>(_appsList)
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
                UIElement.OpacityProperty,
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

    // ── Tile folder animations ────────────────────────────────────

    public async Task ToggleTileFolderAsync(TileGroup group, TileItem folder)
    {
        if (_isTileFolderAnimating)
        {
            return;
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            folder.IsFolderExpanded = !folder.IsFolderExpanded;
            group.RefreshLayout();
            _window.UpdateLayout();
            return;
        }

        _isTileFolderAnimating = true;
        var generation = ++_tileFolderAnimationGeneration;
        try
        {
            if (!folder.IsFolderExpanded)
            {
                var expandPreviousTops = group.Tiles.ToDictionary(item => item, item => item.DisplayTop);
                var expandPreviousGroupPositions = _tileDragCoordinator.CaptureGroupReorderPositions();
                folder.IsFolderExpanded = true;
                group.RefreshLayout();
                _window.UpdateLayout();
                var shiftDuration = AnimateTileFolderShift(group, expandPreviousTops, expanding: true);
                var expandMovedGroups = _tileDragCoordinator.AnimateGroupReorderFrom(expandPreviousGroupPositions);
                AnimateTileFolderRegion(folder, expanding: true);
                await Task.Delay(Math.Max(
                    Math.Max(shiftDuration, Win10FolderMotion.TileRegionExpandDurationMilliseconds),
                    expandMovedGroups.Count == 0 ? 0 : Win10ReorderMotion.DurationMilliseconds));
                return;
            }

            var collapsePreviousTops = group.Tiles.ToDictionary(item => item, item => item.DisplayTop);
            var collapsePreviousGroupPositions = _tileDragCoordinator.CaptureGroupReorderPositions();
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
            _window.UpdateLayout();
            var collapseDuration = AnimateTileFolderShift(group, collapsePreviousTops, expanding: false);
            var collapseMovedGroups = _tileDragCoordinator.AnimateGroupReorderFrom(collapsePreviousGroupPositions);
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
                collapseRegion.ClearValue(UIElement.VisibilityProperty);
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
        var groupControl = FindVisualDescendants<ItemsControl>(_tileGroupsControl)
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
            UIElement.OpacityProperty,
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
        FindVisualDescendants<System.Windows.Controls.Border>(_tileGroupsControl)
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

    public bool FindTileLocation(TileItem tile, out TileGroup group, out TileItem? folder)
    {
        foreach (var candidate in _tileLayout.Groups)
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

    public static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
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