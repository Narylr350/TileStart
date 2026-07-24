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
    private void StartContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _openContextMenuCount++;
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
                    item.Visibility = AppLauncher.CanOpenFileLocation(tile, _launchableApps)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (item.Tag as string == "DissolveFolder")
                {
                    item.Visibility = tile.IsTileFolder ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (item.Tag as string == "Uninstall")
                {
                    item.Visibility = AppUninstaller.CanUninstall(tile, _launchableApps)
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

    private static IEnumerable<MenuItem> EnumerateMenuItems(ItemsControl owner)
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

    private void SubmenuPopup_Opened(object? sender, EventArgs e)
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
                border.ClearValue(ClipProperty);
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

    private void SubmenuPopup_Closed(object? sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Popup
            {
                Child: System.Windows.Controls.Border border,
            })
        {
            border.ClearValue(ClipProperty);
        }
    }

    private void StartContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _openContextMenuCount = Math.Max(0, _openContextMenuCount - 1);
        if (sender is ContextMenu menu && GetContextMenuPopupBorder(menu) is { } border)
        {
            border.ClearValue(ClipProperty);
        }

        if (!_navigationPinnedOpen && !NavigationPane.IsMouseOver)
        {
            SetNavigationExpanded(false);
        }

        Dispatcher.BeginInvoke(
            () => TryDismissAfterForegroundChange("context-menu-closed"),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
    private void PinAppToStart_Click(object sender, RoutedEventArgs e)
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

        var tile = CreateAppTile(app);
        PinTileToStart(tile);
    }

    private void UnpinAppFromStart_Click(object sender, RoutedEventArgs e)
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
        var tiles = TileLayout.Groups
            .SelectMany(group => group.Tiles)
            .Where(tile => LaunchTargetIdentity.GetKey(tile.LaunchTarget) == identity)
            .ToArray();
        var changed = false;
        foreach (var tile in tiles)
        {
            changed |= TileContextActions.Unpin(TileLayout, tile);
        }

        if (changed)
        {
            TileLayoutStore.Save(TileLayout);
        }
    }

    private bool IsPinnedToStart(AppEntry app)
    {
        var identity = LaunchTargetIdentity.GetKey(app.LaunchTarget);
        return TileLayout.Groups
            .SelectMany(group => group.Tiles)
            .Any(tile => LaunchTargetIdentity.GetKey(tile.LaunchTarget) == identity);
    }

    private void RemoveCustomApp_Click(object sender, RoutedEventArgs e)
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
        RemoveApplicationFromList(identity);
        ShowIfHidden();
    }

    private bool PinTileToStart(TileItem tile)
    {
        var placement = Win10GroupLayout.FindPinPlacement(TileLayout.Groups, tile)
                        ?? new Win10PinPlacement(
                            TileGroupManager.Add(TileLayout, CurrentGroupColumnCount()),
                            0,
                            0);
        if (Win10GroupLayout.AddToFreeCell(placement.Group, tile, placement.Column, placement.Row))
        {
            TileLayoutStore.Save(TileLayout);
            return true;
        }

        return false;
    }
    private void TileButton_Click(object sender, RoutedEventArgs e)
    {
        var suppress = _dragCompleted || Environment.TickCount64 <= _suppressTileActivationUntil;
        if (suppress)
        {
            _dragCompleted = false;
            return;
        }

        if (sender is not Button { Tag: TileItem tile })
        {
            return;
        }

        if (tile.IsTileFolder)
        {
            var group = TileLayout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(tile));
            if (group is not null)
            {
                _ = ToggleTileFolderAsync(group, tile);
            }

            return;
        }

        if (AppLauncher.Launch(tile))
        {
            DismissWindow(yieldTopmost: true);
        }
    }

    private void TileSettings_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is null)
        {
            return;
        }

        var group = TileLayout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(tile));
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
        RestoreTileIcon(defaultVisual, _launchableApps);
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
            TileContextActions.Unpin(TileLayout, tile);
            TileLayoutStore.Save(TileLayout);
            return;
        }

        ApplyTileSettings(tile, dialog);
        Win10GroupLayout.Normalize(group);
        TileLayoutStore.Save(TileLayout);
    }

    private void UnpinTile_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null && TileContextActions.Unpin(TileLayout, tile))
        {
            TileLayoutStore.Save(TileLayout);
        }
    }

    private void DissolveFolder_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is null)
        {
            return;
        }

        var previousPositions = CaptureReorderPositions();
        if (!TileContextActions.DissolveFolder(TileLayout, tile))
        {
            return;
        }

        UpdateLayout();
        AnimateReorderFrom(previousPositions);
        TileLayoutStore.Save(TileLayout);
    }

    private void ResizeTile_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null
            && sender is MenuItem { Tag: string sizeName }
            && Enum.TryParse<TileSize>(sizeName, out var size)
            && TileContextActions.Resize(TileLayout, tile, size))
        {
            RestoreTileIcon(tile, _launchableApps);
            TileLayoutStore.Save(TileLayout);
        }
    }

    private void OpenTileFileLocation_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null)
        {
            AppLauncher.OpenFileLocation(tile, _launchableApps);
        }
    }

    private void UninstallApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AppEntry app } && AppUninstaller.Open(app))
        {
            DismissWindow(yieldTopmost: true);
        }
    }

    private void UninstallTile_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null && AppUninstaller.Open(tile, _launchableApps))
        {
            DismissWindow(yieldTopmost: true);
        }
    }

    private async void PinAppToTaskbar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: AppEntry app })
        {
            await RequestTaskbarPinAsync(app);
        }
    }

    private async void PinTileToTaskbar_Click(object sender, RoutedEventArgs e)
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
            DismissWindow(yieldTopmost: true);
            return;
        }

        ShowTaskbarPinFailed();
    }

    private async Task RequestTaskbarPinAsync(AppEntry app)
    {
        if (await TaskbarPinner.RequestPinAsync(app))
        {
            DismissWindow(yieldTopmost: true);
            return;
        }

        ShowTaskbarPinFailed();
    }

    private void ShowTaskbarPinFailed() => System.Windows.MessageBox.Show(this,
            "Windows 没有允许固定该应用，或该应用已经固定到任务栏。",
            "固定到任务栏",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    private AppEntry? FindApp(TileItem tile) => _launchableApps.FirstOrDefault(candidate =>
        candidate.LaunchTarget.Equals(tile.LaunchTarget, StringComparison.OrdinalIgnoreCase));

    private void RunTileAsAdministrator_Click(object sender, RoutedEventArgs e)
    {
        var tile = GetContextTile(sender);
        if (tile is not null && AppLauncher.LaunchAsAdministrator(tile))
        {
            DismissWindow(yieldTopmost: true);
        }
    }

    private static TileItem? GetContextTile(object sender)
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

    private void AddCommandTile_Click(object sender, RoutedEventArgs e)
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
        var placement = Win10GroupLayout.FindPinPlacement(TileLayout.Groups, tile)
                        ?? new Win10PinPlacement(
                            TileGroupManager.Add(TileLayout, CurrentGroupColumnCount()),
                            0,
                            0);
        if (Win10GroupLayout.AddToFreeCell(placement.Group, tile, placement.Column, placement.Row))
        {
            TileLayoutStore.Save(TileLayout);
        }
    }

    private void GroupHeader_NameCommitted(object sender, EventArgs e)
    {
        TileLayoutStore.Save(TileLayout);
    }
    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        var group = GetContextGroup(sender);
        if (group is null)
        {
            return;
        }

        if (group.Tiles.Count > 0
            && System.Windows.MessageBox.Show(this,
                "删除该组会同时取消固定其中的全部磁贴。是否继续？",
                "删除组",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (TileGroupManager.Remove(TileLayout, group))
        {
            TileLayoutStore.Save(TileLayout);
        }
    }

    private void GroupSettings_Click(object sender, RoutedEventArgs e)
    {
        var group = GetContextGroup(sender);
        if (group is null)
        {
            return;
        }

        GroupSettingsWindow dialog;
        try
        {
            dialog = new GroupSettingsWindow(group, _launchableApps);
            if (ShowGroupSettingsDialog(dialog) != true)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Unable to open group settings: {exception}");
            System.Windows.MessageBox.Show(
                this,
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
            .Select(option => option.ExistingTile ?? CreateAppTile(option.App!))
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
                this,
                "组内容无法应用到所选尺寸，原布局已恢复。",
                "TileStart",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        EnsureGroupGridCoordinates();
        RefreshGroupPanelLayout();
        TileLayoutStore.Save(TileLayout);
    }

    private static TileGroup? GetContextGroup(object sender)
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
        RestoreTileIcon(tile, _launchableApps);
        tile.BackgroundImage = ShellIconLoader.LoadImage(tile.BackgroundImagePath);
    }

    private bool? ShowTileSettingsDialog(TileSettingsWindow dialog)
    {
        var wasTopmost = Topmost;
        Topmost = false;
        dialog.Owner = this;
        try
        {
            return dialog.ShowDialog();
        }
        finally
        {
            Topmost = wasTopmost;
        }
    }

    private bool? ShowGroupSettingsDialog(GroupSettingsWindow dialog)
    {
        var wasTopmost = Topmost;
        Topmost = false;
        dialog.Owner = this;
        try
        {
            return dialog.ShowDialog();
        }
        finally
        {
            Topmost = wasTopmost;
        }
    }
}
