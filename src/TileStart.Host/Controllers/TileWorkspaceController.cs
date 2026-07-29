using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using Canvas = System.Windows.Controls.Canvas;
using ContextMenu = System.Windows.Controls.ContextMenu;
using ItemsControl = System.Windows.Controls.ItemsControl;
using MenuItem = System.Windows.Controls.MenuItem;
using TileStart.Host.Applications;
using TileStart.Host.Icons;
using TileStart.Host.Shell;
using TileStart.Host.Navigation;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Tiles.Layout;
using TileStart.Host.Tiles.DragDrop;
using TileStart.Host.Tiles.Settings;
using TileStart.Host.Persistence;
using TileStart.Host.Utilities;
using TileStart.Host.Tiles.Folders;
using TileStart.Host.Windowing;

namespace TileStart.Host.Controllers;

internal sealed class TileWorkspaceController : IDisposable
{
    private int _appFolderAnimationGeneration;
    private int _tileFolderAnimationGeneration;
    private bool _isAppFolderAnimating;
    private bool _isTileFolderAnimating;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private bool _isDisposed;

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
    private readonly Action<bool> _setOpenContextMenuState;
    private readonly Func<long> _getSuppressTileActivationUntil;
    private ContextMenu? _openContextMenu;

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
        Action<bool> setOpenContextMenuState,
        Func<long> getSuppressTileActivationUntil)
    {
        _lifetimeToken = _lifetimeCancellation.Token;
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
        _setOpenContextMenuState = setOpenContextMenuState;
        _getSuppressTileActivationUntil = getSuppressTileActivationUntil;
    }

    public TileLayout GetTileLayout() => _tileLayout;

    // ── Context menu ──────────────────────────────────────────────

    public void StartContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _setOpenContextMenuState(true);
        if (sender is not ContextMenu menu)
        {
            return;
        }

        _openContextMenu = menu;
        MenuPopupAnimator.OpenTopLevel(menu);

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
                if (item.Tag as string is "PinStart" or "UnpinStart")
                {
                    if (app.IsFolder)
                    {
                        item.Visibility = Visibility.Collapsed;
                        continue;
                    }

                    var isPinned = IsPinnedToStart(app);
                    item.Visibility = item.Tag as string == "PinStart"
                        ? (isPinned ? Visibility.Collapsed : Visibility.Visible)
                        : (isPinned ? Visibility.Visible : Visibility.Collapsed);
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

    public void StartContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _setOpenContextMenuState(false);
        if (ReferenceEquals(_openContextMenu, sender))
        {
            _openContextMenu = null;
        }

        if (sender is ContextMenu menu)
        {
            // A submenu is a separate Popup HWND. When WPF closes the top-level menu
            // because focus moved elsewhere, it does not reliably reset the nested
            // MenuItem state used by our custom template, so close the whole popup tree.
            CloseSubmenus(menu);
            MenuPopupAnimator.CloseTopLevel(menu);
        }

        if (!_navigationController.IsNavigationPinnedOpen && !_navigationPane.IsMouseOver)
        {
            _navigationController.SetNavigationExpanded(false);
        }

        _window.Dispatcher.BeginInvoke(
            () =>
            {
                if (!_isDisposed)
                {
                    _tryDismissAfterForegroundChange("context-menu-closed");
                }
            },
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    public void CloseOpenContextMenu()
    {
        var menu = _openContextMenu;
        _openContextMenu = null;
        CloseContextMenu(menu, _setOpenContextMenuState);
    }

    internal static void CloseContextMenu(ContextMenu? menu, Action<bool> setOpenContextMenuState)
    {
        setOpenContextMenuState(false);
        if (menu is null)
        {
            return;
        }

        CloseSubmenus(menu);
        menu.IsOpen = false;
    }

    internal static void CloseSubmenus(ItemsControl owner)
    {
        foreach (var item in EnumerateMenuItems(owner))
        {
            item.IsSubmenuOpen = false;
        }
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

        if (_tileLayout.ContainsLaunchTarget(app.LaunchTarget))
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
        if (_tileLayout.ContainsLaunchTarget(tile.LaunchTarget))
        {
            return false;
        }

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

        if (!FindTileLocation(tile, out var group, out var folder))
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

        void CommitSettings()
        {
            ApplyTileSettings(tile, dialog);
            if (folder is null)
            {
                Win10GroupLayout.Normalize(group);
            }
            else
            {
                TileFolderLayout.Normalize(folder);
                group.RefreshLayout();
                _window.UpdateLayout();
            }

            TileLayoutStore.Save(_tileLayout);
        }

        dialog.ApplyRequested += (_, _) => CommitSettings();
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

        CommitSettings();
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

    public void FolderContents_Click(object sender, RoutedEventArgs e)
    {
        var folder = GetContextTile(sender);
        if (folder?.IsTileFolder != true)
        {
            return;
        }

        var group = _tileLayout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(folder));
        if (group is null)
        {
            return;
        }

        var excludedTargets = _tileLayout.EnumerateTiles()
            .Where(tile => !folder.FolderTiles.Contains(tile))
            .Where(tile => !string.IsNullOrWhiteSpace(tile.LaunchTarget))
            .Select(tile => LaunchTargetIdentity.GetKey(tile.LaunchTarget))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dialog = new GroupSettingsWindow(folder, _appController.LaunchableApps, excludedTargets);
        if (ShowGroupSettingsDialog(dialog) != true)
        {
            return;
        }

        var selectedTiles = dialog.SelectedOptions
            .Select(option => option.ExistingTile ?? _appController.CreateAppTile(option.App!))
            .Where(tile => !tile.IsTileFolder)
            .GroupBy(TileLayout.GetIdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(grouping => grouping.First())
            .ToArray();
        folder.FolderTiles.Clear();
        foreach (var tile in selectedTiles)
        {
            folder.FolderTiles.Add(tile);
        }

        TileFolderLayout.Normalize(folder);
        group.RefreshLayout();
        _window.UpdateLayout();
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

    private void ShowTaskbarPinFailed() => TileStartMessageDialog.Show(
        _window,
        "无法固定到任务栏",
        "Windows 没有允许固定该应用，或该应用已经固定到任务栏。",
        TileStartMessageKind.Information);

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

    public void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = CreateEmptyFolder();
        var placement = Win10GroupLayout.FindPinPlacement(_tileLayout.Groups, folder)
                        ?? new Win10PinPlacement(
                            TileGroupManager.Add(_tileLayout, _tileDragCoordinator.CurrentGroupColumnCount()),
                            0,
                            0);
        if (!Win10GroupLayout.AddToFreeCell(placement.Group, folder, placement.Column, placement.Row))
        {
            TileStartMessageDialog.Show(
                _window,
                "无法新建文件夹",
                "当前布局没有可容纳新文件夹的空间。",
                TileStartMessageKind.Warning);
            return;
        }

        _tileDragCoordinator.EnsureGroupGridCoordinates();
        _tileDragCoordinator.RefreshGroupPanelLayout();
        TileLayoutStore.Save(_tileLayout);
    }

    internal static TileItem CreateEmptyFolder() => new()
    {
        Name = "文件夹",
        IsTileFolder = true,
        Size = TileSize.Medium,
    };

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
            && !TileStartMessageDialog.Confirm(
                _window,
                "删除组",
                "删除该组会同时取消固定其中的全部磁贴。是否继续？",
                TileStartMessageKind.Warning,
                primaryText: "删除组",
                secondaryText: "取消"))
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
            var excludedTargets = _tileLayout.EnumerateTiles()
                .Where(tile => !group.Tiles.Contains(tile))
                .Select(tile => LaunchTargetIdentity.GetKey(tile.LaunchTarget))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            dialog = new GroupSettingsWindow(group, _appController.LaunchableApps, excludedTargets);
            if (ShowGroupSettingsDialog(dialog) != true)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Unable to open group settings: {exception}");
            TileStartMessageDialog.Show(
                _window,
                "无法打开组设置",
                "错误已写入 TileStart 日志。",
                TileStartMessageKind.Error);
            return;
        }

        ApplyGroupSettings(group, dialog);
    }

    private void ApplyGroupSettings(TileGroup group, GroupSettingsWindow dialog)
    {
        var previousName = group.Name;
        var previousWidthUnits = group.WidthUnits;
        var previousHeightUnits = group.HeightUnits;
        var previousTiles = group.Tiles.ToArray();
        var selectedTiles = dialog.SelectedOptions
            .Select(option => option.ExistingTile ?? _appController.CreateAppTile(option.App!))
            .GroupBy(TileLayout.GetIdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(grouping => grouping.First())
            .ToList();
        PreserveStructuralFolders(previousTiles, selectedTiles);
        var requiresReflow = previousWidthUnits != dialog.WidthUnits
                             || previousHeightUnits != dialog.HeightUnits
                             || !previousTiles.SequenceEqual(selectedTiles);

        group.Name = dialog.GroupName;
        group.WidthUnits = dialog.WidthUnits;
        group.HeightUnits = dialog.HeightUnits;
        group.Tiles.Clear();
        foreach (var tile in selectedTiles)
        {
            group.Tiles.Add(tile);
        }

        // Existing tile coordinates remain valid when a group becomes wider, so Normalize
        // alone preserves the old narrow arrangement. Reflow whenever the grid definition
        // or visual item order changes, making the saved layout match the live preview.
        var applied = requiresReflow
            ? Win10GroupLayout.Reflow(group)
            : Win10GroupLayout.Normalize(group);
        if (!applied)
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
            TileStartMessageDialog.Show(
                _window,
                "无法应用组布局",
                "组内容无法放入所选尺寸，原布局已恢复。",
                TileStartMessageKind.Warning);
            return;
        }

        _tileDragCoordinator.EnsureGroupGridCoordinates();
        _tileDragCoordinator.RefreshGroupPanelLayout();
        TileLayoutStore.Save(_tileLayout);
    }

    internal static void PreserveStructuralFolders(
        IReadOnlyList<TileItem> previousTiles,
        IList<TileItem> selectedTiles)
    {
        for (var index = 0; index < previousTiles.Count; index++)
        {
            var folder = previousTiles[index];
            if (!folder.IsTileFolder || selectedTiles.Contains(folder))
            {
                continue;
            }

            selectedTiles.Insert(Math.Min(index, selectedTiles.Count), folder);
        }
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
        tile.BackgroundImagePath = dialog.BackgroundImagePath;
        tile.BackgroundImageScale = dialog.BackgroundImageScale;
        // Saving the theme's own colour means "no custom colour", so the tile keeps following
        // the active theme instead of freezing today's value.
        if (string.Equals(dialog.BackgroundColor, TileItem.ThemeDefaultBackgroundColor,
                StringComparison.OrdinalIgnoreCase))
        {
            tile.ClearCustomBackgroundColor();
        }
        else
        {
            tile.BackgroundColor = dialog.BackgroundColor;
        }

        tile.ForegroundColor = dialog.ForegroundColor;
        tile.ShowTitle = dialog.ShowTitle;
        tile.BackgroundImage = ShellIconLoader.LoadImage(tile.BackgroundImagePath);

        if (tile.IsTileFolder)
        {
            return;
        }

        tile.LaunchTarget = dialog.LaunchTarget;
        tile.Arguments = dialog.Arguments;
        tile.WorkingDirectory = dialog.WorkingDirectory;
        tile.IconPath = dialog.IconPath;
        tile.IconSourceKind = dialog.IconSourceKind;
        tile.IconSourceValue = dialog.IconSourceValue;
        tile.IconSize = dialog.IconSize;
        tile.IconPosition = dialog.IconPosition;
        tile.RunAsAdministrator = dialog.RunAsAdministrator;
        tile.Size = dialog.TileSize;
        ApplicationPaneController.RestoreTileIcon(tile, _appController.LaunchableApps);
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
        if (_isDisposed || _isAppFolderAnimating)
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
                if (!await WaitForAnimationAsync(Win10FolderMotion.AppOpenDuration(folder.Children.Count)))
                {
                    return;
                }

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
            if (!await WaitForAnimationAsync(Win10FolderMotion.AppChildDurationMilliseconds))
            {
                return;
            }

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
        if (_isDisposed || _isTileFolderAnimating)
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
        TileFolderPreviewTransition? previewTransition = null;
        try
        {
            if (!folder.IsFolderExpanded)
            {
                previewTransition = PrepareTileFolderPreviewTransition(folder, expanding: true);
                var expandPreviousTops = group.Tiles.ToDictionary(item => item, item => item.DisplayTop);
                var expandPreviousGroupPositions = _tileDragCoordinator.CaptureGroupReorderPositions();
                folder.IsFolderExpanded = true;
                group.RefreshLayout();
                _window.UpdateLayout();
                var shiftDuration = AnimateTileFolderShift(group, expandPreviousTops, expanding: true);
                var expandMovedGroups = _tileDragCoordinator.AnimateGroupReorderFrom(expandPreviousGroupPositions);
                var childDuration = AnimateTileFolderChildren(folder);
                var totalDuration = Math.Max(
                    Math.Max(
                        Math.Max(shiftDuration, childDuration),
                        Win10FolderMotion.TileDecorationDelayMilliseconds
                        + Win10FolderMotion.TileDecorationDurationMilliseconds),
                    expandMovedGroups.Count == 0 ? 0 : Win10ReorderMotion.DurationMilliseconds);
                if (!await WaitForAnimationAsync(totalDuration))
                {
                    return;
                }

                previewTransition?.Complete();
                previewTransition = null;
                return;
            }

            previewTransition = PrepareTileFolderPreviewTransition(folder, expanding: false);
            var collapsePreviousTops = group.Tiles.ToDictionary(item => item, item => item.DisplayTop);
            var collapsePreviousGroupPositions = _tileDragCoordinator.CaptureGroupReorderPositions();
            folder.IsFolderExpanded = false;
            group.RefreshLayout();
            _window.UpdateLayout();
            var collapseDuration = AnimateTileFolderShift(group, collapsePreviousTops, expanding: false);
            var collapseMovedGroups = _tileDragCoordinator.AnimateGroupReorderFrom(collapsePreviousGroupPositions);
            var totalCollapseDuration = Math.Max(
                Math.Max(collapseDuration, Win10FolderMotion.TilePreviewEnterDurationMilliseconds),
                collapseMovedGroups.Count == 0 ? 0 : Win10ReorderMotion.DurationMilliseconds);
            if (!await WaitForAnimationAsync(totalCollapseDuration))
            {
                return;
            }

            previewTransition?.Complete();
            previewTransition = null;
        }
        finally
        {
            previewTransition?.Complete();
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

    private int AnimateTileFolderChildren(TileItem folder)
    {
        var region = FindTileFolderRegion(folder);
        var control = region is null
            ? null
            : FindVisualDescendants<ItemsControl>(region)
                .FirstOrDefault(candidate => ReferenceEquals(candidate.Tag, folder));
        if (control is null)
        {
            return 0;
        }

        control.UpdateLayout();
        var children = folder.FolderTiles
            .OrderBy(tile => tile.Row)
            .ThenBy(tile => tile.Column)
            .ToArray();
        var rows = children.Select(tile => tile.Row).Distinct().Order().ToArray();
        var columns = children.Select(tile => tile.Column).Distinct().Order().ToArray();
        var maximumDuration = 0;
        foreach (var tile in children)
        {
            if (control.ItemContainerGenerator.ContainerFromItem(tile) is not FrameworkElement child)
            {
                continue;
            }

            var rowIndex = Array.IndexOf(rows, tile.Row);
            var columnIndex = Array.IndexOf(columns, tile.Column);
            var delay = Win10FolderMotion.TileChildWaveDelay(
                rowIndex,
                columnIndex,
                rows.Length,
                columns.Length);
            var transform = new TranslateTransform();
            child.RenderTransform = transform;
            child.Opacity = 1;

            // 原版从第一次露出起就是完整尺寸磁贴，并已位于最终列；
            // 这里只把它放到展开区顶部之外，再靠父区域裁切向下进入。
            transform.BeginAnimation(
                TranslateTransform.YProperty,
                Win10FolderMotion.CreateSplineAnimation(
                    -(tile.Top + tile.PixelHeight),
                    0,
                    delay,
                    Win10FolderMotion.TileChildDurationMilliseconds,
                    Win10FolderMotion.StandardSpline),
                HandoffBehavior.SnapshotAndReplace);
            maximumDuration = Math.Max(
                maximumDuration,
                delay + Win10FolderMotion.TileChildDurationMilliseconds);
        }

        return maximumDuration;
    }

    private TileFolderPreviewTransition? PrepareTileFolderPreviewTransition(
        TileItem folder,
        bool expanding)
    {
        var preview = FindVisualDescendants<ItemsControl>(_tileGroupsControl)
            .FirstOrDefault(control =>
                control.Name == "FolderPreview" && ReferenceEquals(control.DataContext, folder));
        if (preview is null)
        {
            return null;
        }

        var glyph = FindVisualDescendants<FrameworkElement>(_tileGroupsControl)
            .FirstOrDefault(element =>
                element.Name == "FolderCollapseGlyph" && ReferenceEquals(element.DataContext, folder));
        preview.BeginAnimation(UIElement.OpacityProperty, null);
        preview.Visibility = Visibility.Visible;
        preview.Opacity = 1;
        var transform = new TranslateTransform();
        preview.RenderTransform = transform;
        var travel = folder.PixelHeight - Win10VisualMetrics.TileReservedBrandingSpace;
        var duration = expanding
            ? Win10FolderMotion.TilePreviewExitDurationMilliseconds
            : Win10FolderMotion.TilePreviewEnterDurationMilliseconds;
        transform.BeginAnimation(
            TranslateTransform.YProperty,
            Win10FolderMotion.CreateSplineAnimation(
                expanding ? 0 : travel,
                expanding ? travel : 0,
                0,
                duration,
                Win10FolderMotion.StandardSpline,
                FillBehavior.HoldEnd),
            HandoffBehavior.SnapshotAndReplace);
        preview.BeginAnimation(
            UIElement.OpacityProperty,
            Win10FolderMotion.CreateSplineAnimation(
                expanding ? 1 : 0,
                expanding ? 0 : 1,
                0,
                duration,
                Win10FolderMotion.StandardSpline,
                FillBehavior.HoldEnd),
            HandoffBehavior.SnapshotAndReplace);

        if (glyph is not null)
        {
            glyph.BeginAnimation(UIElement.OpacityProperty, null);
            if (expanding)
            {
                glyph.Visibility = Visibility.Visible;
                glyph.Opacity = 1;
                glyph.BeginAnimation(
                    UIElement.OpacityProperty,
                    Win10FolderMotion.CreateSplineAnimation(
                        0,
                        1,
                        Win10FolderMotion.TileDecorationDelayMilliseconds,
                        Win10FolderMotion.TileDecorationDurationMilliseconds,
                        Win10FolderMotion.StandardSpline,
                        FillBehavior.HoldEnd),
                    HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                glyph.Visibility = Visibility.Collapsed;
            }
        }

        return new TileFolderPreviewTransition(preview, glyph);
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

    private async Task<bool> WaitForAnimationAsync(int milliseconds)
    {
        try
        {
            await Task.Delay(milliseconds, _lifetimeToken);
            return true;
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private sealed class TileFolderPreviewTransition(
        ItemsControl preview,
        FrameworkElement? glyph)
    {
        private bool _completed;

        public void Complete()
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            if (preview.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
            }

            preview.BeginAnimation(UIElement.OpacityProperty, null);
            preview.ClearValue(UIElement.OpacityProperty);
            preview.RenderTransform = null;
            preview.ClearValue(UIElement.VisibilityProperty);
            if (glyph is not null)
            {
                glyph.BeginAnimation(UIElement.OpacityProperty, null);
                glyph.ClearValue(UIElement.OpacityProperty);
                glyph.ClearValue(UIElement.VisibilityProperty);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _appFolderAnimationGeneration++;
        _tileFolderAnimationGeneration++;
        _isAppFolderAnimating = false;
        _isTileFolderAnimating = false;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }
}