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
    private readonly RangeObservableCollection<AppEntry> _apps = [];
    private readonly Queue<HostRequest> _pendingHostRequests = [];
    private AppEntry[] _launchableApps = [];
    private AppEntry[] _recentAppCandidates = [];
    private bool _applicationContentReady;

    private async Task LoadAppsAsync()
    {
        try
        {
            var scannedApps = FilterHiddenApplications(
                await StartAppScanner.ScanAsync(),
                AppVisibilityStore.Load());
            var customApps = CustomAppStore.Load();
            var scannedLaunchableApps = AppEntry.FlattenApplications(scannedApps).ToArray();
            var apps = customApps.Count == 0
                ? scannedApps.ToArray()
                : scannedApps
                    .Concat(customApps.Where(custom => !scannedLaunchableApps.Any(existing =>
                        LaunchTargetIdentity.GetKey(existing.LaunchTarget)
                        == LaunchTargetIdentity.GetKey(custom.LaunchTarget))))
                    .ToArray();
            _apps.AddRange(apps);

            var launchableApps = AppEntry.FlattenApplications(apps).ToArray();
            _launchableApps = launchableApps;
            _recentAppCandidates = launchableApps
                .Where(app => app.AddedAt > DateTime.MinValue)
                .OrderByDescending(app => app.AddedAt)
                .Take(ExpandedRecentAppCount)
                .ToArray();
            RefreshRecentApps();
            RecentExpandButton.Visibility = _recentAppCandidates.Length > CollapsedRecentAppCount
                ? Visibility.Visible
                : Visibility.Collapsed;

            AlphabetIndex.UpdateAvailability(AlphabetLetters, apps, RecentApps.Count > 0);
            var savedLayout = TileLayoutStore.Load();
            var layout = savedLayout ?? DefaultTileLayout.Create(launchableApps);
            RestoreTileIcons(layout, launchableApps);
            foreach (var group in layout.Groups)
            {
                TileLayout.Groups.Add(group);
            }

            UpdateLayout();
            var migratedGroupCoordinates = EnsureGroupGridCoordinates();
            if (savedLayout is null || migratedGroupCoordinates)
            {
                TileLayoutStore.Save(TileLayout);
            }

            PrepareMotionElements();
            DiagnosticLog.Write("Application content ready.");
            QueueContextMenuPrewarm();
            _ = LoadApplicationIconsAsync(launchableApps);
            _applicationContentReady = true;
            while (_pendingHostRequests.Count > 0)
            {
                HandleHostRequest(_pendingHostRequests.Dequeue());
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application list load failed: {exception}");
        }
    }

    public void HandleHostRequest(HostRequest request)
    {
        if (!_applicationContentReady)
        {
            _pendingHostRequests.Enqueue(request);
            return;
        }

        switch (request.Kind)
        {
            case HostRequestKind.AddToAppList:
                AddExternalApplication(request.Path);
                break;
            case HostRequestKind.PinTile:
                PinExternalTile(request.Path);
                break;
        }
    }

    private void AddExternalApplication(string path)
    {
        var identity = LaunchTargetIdentity.GetKey(path);
        AppVisibilityStore.Show(identity);
        var app = CustomAppStore.Add(path);
        if (app is null)
        {
            return;
        }

        if (_launchableApps.Any(existing => LaunchTargetIdentity.GetKey(existing.LaunchTarget) == identity))
        {
            ShowIfHidden();
            return;
        }

        _apps.Add(app);
        RefreshApplicationCollection();
        _ = LoadApplicationIconsAsync([app]);
        ShowIfHidden();
    }

    private void PinExternalTile(string path)
    {
        var tile = DroppedTileFactory.Create(path);
        if (tile is null || tile.TargetType != TileTargetType.Application)
        {
            return;
        }

        var identity = LaunchTargetIdentity.GetKey(tile.LaunchTarget);
        if (!CustomAppStore.Contains(path))
        {
            AppVisibilityStore.Hide(identity);
            RemoveApplicationFromList(identity);
        }

        if (TileLayout.Groups.SelectMany(group => group.Tiles)
            .Any(existing => LaunchTargetIdentity.GetKey(existing.LaunchTarget) == identity))
        {
            ShowIfHidden();
            return;
        }

        if (PinTileToStart(tile))
        {
            ShowIfHidden();
        }
    }

    private void ShowIfHidden()
    {
        if (!IsVisible)
        {
            ShowFromShell();
        }
    }

    private static IReadOnlyList<AppEntry> FilterHiddenApplications(
        IEnumerable<AppEntry> entries,
        IReadOnlySet<string> hiddenIdentities)
    {
        var visible = new List<AppEntry>();
        foreach (var entry in entries)
        {
            if (entry.IsFolder)
            {
                var visibleChildren = FilterHiddenApplications(entry.Children, hiddenIdentities);
                entry.Children.Clear();
                foreach (var child in visibleChildren)
                {
                    entry.Children.Add(child);
                }

                if (entry.Children.Count > 0)
                {
                    visible.Add(entry);
                }
            }
            else if (!hiddenIdentities.Contains(LaunchTargetIdentity.GetKey(entry.LaunchTarget)))
            {
                visible.Add(entry);
            }
        }

        return visible;
    }

    private void RemoveApplicationFromList(string identity)
    {
        if (RemoveApplicationsByIdentity(_apps, identity))
        {
            RefreshApplicationCollection();
        }
    }

    private static bool RemoveApplicationsByIdentity(IList<AppEntry> entries, string identity)
    {
        var removed = false;
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];
            if (entry.IsFolder)
            {
                removed |= RemoveApplicationsByIdentity(entry.Children, identity);
                if (entry.Children.Count == 0)
                {
                    entries.RemoveAt(index);
                }
            }
            else if (LaunchTargetIdentity.GetKey(entry.LaunchTarget) == identity)
            {
                entries.RemoveAt(index);
                removed = true;
            }
        }

        return removed;
    }

    private static bool RemoveMissingApplications(IList<AppEntry> entries)
    {
        var removed = false;
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];
            if (entry.IsFolder)
            {
                removed |= RemoveMissingApplications(entry.Children);
                if (entry.Children.Count == 0)
                {
                    entries.RemoveAt(index);
                    removed = true;
                }
            }
            else if (IsMissingFileApplication(entry))
            {
                entries.RemoveAt(index);
                removed = true;
            }
        }

        return removed;
    }

    private static bool IsMissingFileApplication(AppEntry app)
    {
        if (app.LaunchTarget.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return Path.IsPathFullyQualified(app.LaunchTarget) && !File.Exists(app.LaunchTarget);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return true;
        }
    }

    private void RefreshApplicationCollection()
    {
        _launchableApps = [.. AppEntry.FlattenApplications(_apps)];
        _recentAppCandidates = _launchableApps
            .Where(app => app.AddedAt > DateTime.MinValue)
            .OrderByDescending(app => app.AddedAt)
            .Take(ExpandedRecentAppCount)
            .ToArray();
        RefreshRecentApps();
        RecentExpandButton.Visibility = _recentAppCandidates.Length > CollapsedRecentAppCount
            ? Visibility.Visible
            : Visibility.Collapsed;
        AppsView.Refresh();
        AlphabetIndex.UpdateAvailability(AlphabetLetters, _apps, RecentApps.Count > 0);
    }

    private void QueueContextMenuPrewarm()
    {
        Dispatcher.BeginInvoke(
            PrewarmContextMenus,
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void PrewarmContextMenus()
    {
        if (IsVisible)
        {
            return;
        }

        var startedAt = Environment.TickCount64;
        var owners = new List<Button> { NavigationToggleButton };
        var appOwner = FindVisualDescendants<Button>(WindowRoot)
            .FirstOrDefault(button => button.ContextMenu is not null && button.Tag is AppEntry { IsFolder: false });
        var tileOwner = FindVisualDescendants<Button>(WindowRoot)
            .FirstOrDefault(button => button.ContextMenu is not null && button.Tag is TileItem);
        if (appOwner is not null)
        {
            owners.Add(appOwner);
        }

        if (tileOwner is not null)
        {
            owners.Add(tileOwner);
        }

        var prewarmed = 0;
        foreach (var owner in owners.Distinct())
        {
            if (PrewarmContextMenu(owner))
            {
                prewarmed++;
            }
        }

        DiagnosticLog.Write(
            $"Context menu prewarm completed: {prewarmed} menus in {Environment.TickCount64 - startedAt} ms.");
    }

    private static bool PrewarmContextMenu(Button owner)
    {
        var menu = owner.ContextMenu;
        if (menu is null)
        {
            return false;
        }

        var placement = menu.Placement;
        var placementTarget = menu.PlacementTarget;
        var horizontalOffset = menu.HorizontalOffset;
        var verticalOffset = menu.VerticalOffset;
        var opacity = menu.Opacity;
        try
        {
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            menu.PlacementTarget = owner;
            menu.HorizontalOffset = -32000;
            menu.VerticalOffset = -32000;
            menu.Opacity = 0;
            menu.IsOpen = true;
            menu.UpdateLayout();

            var submenu = EnumerateMenuItems(menu)
                .FirstOrDefault(item => item.HasItems && item.Visibility == Visibility.Visible);
            if (submenu is not null)
            {
                submenu.IsSubmenuOpen = true;
                submenu.UpdateLayout();
                submenu.IsSubmenuOpen = false;
            }

            return true;
        }
        finally
        {
            menu.IsOpen = false;
            menu.Opacity = opacity;
            menu.HorizontalOffset = horizontalOffset;
            menu.VerticalOffset = verticalOffset;
            menu.PlacementTarget = placementTarget;
            menu.Placement = placement;
        }
    }

    private async Task LoadApplicationIconsAsync(IReadOnlyList<AppEntry> apps)
    {
        try
        {
            var classicApps = apps.Where(app => string.IsNullOrWhiteSpace(app.AppUserModelId)).ToArray();
            var packagedApps = apps.Where(app => !string.IsNullOrWhiteSpace(app.AppUserModelId)).ToArray();
            await Task.WhenAll(
                Task.Run(() => LoadApplicationIcons(classicApps)),
                RunStaThreadAsync(() => LoadApplicationIcons(packagedApps), "TileStart Packaged Icon Loader"));
            DiagnosticLog.Write($"Application icon loading completed: {apps.Count} entries processed.");
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application icon load failed: {exception}");
        }
    }

    private void LoadApplicationIcons(IEnumerable<AppEntry> apps)
    {
        foreach (var app in apps)
        {
            try
            {
                var icon = ShellIconLoader.Load(app.LaunchTarget) ?? GenericAppIcon.Image;

                if (Dispatcher.HasShutdownStarted)
                {
                    return;
                }

                Dispatcher.Invoke(() => ApplyApplicationIcon(app, icon));
            }
            catch (Exception exception)
            {
                if (Dispatcher.HasShutdownStarted)
                {
                    return;
                }

                DiagnosticLog.Write($"Application icon load failed for '{app.LaunchTarget}': {exception.Message}");
            }
        }
    }

    private void ApplyApplicationIcon(AppEntry app, ImageSource icon)
    {
        app.Icon = icon;
        foreach (var tile in TileLayout.Groups.SelectMany(group => group.Tiles))
        {
            ApplyApplicationIconToTile(tile, app.LaunchTarget, icon);
        }
    }

    private static void ApplyApplicationIconToTile(TileItem tile, string launchTarget, ImageSource icon)
    {
        if (string.IsNullOrWhiteSpace(tile.IconPath) &&
            !tile.UsesFullTileLogo &&
            tile.LaunchTarget.Equals(launchTarget, StringComparison.OrdinalIgnoreCase))
        {
            tile.Icon = icon;
        }

        foreach (var child in tile.FolderTiles)
        {
            ApplyApplicationIconToTile(child, launchTarget, icon);
        }
    }

    private static Task RunStaThreadAsync(Action action, string name)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = name,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private void RecentExpandButton_Click(object sender, RoutedEventArgs e)
    {
        _recentAppsExpanded = !_recentAppsExpanded;
        RefreshRecentApps();
    }

    private void RefreshRecentApps()
    {
        RecentApps.Clear();
        foreach (var app in _recentAppCandidates.Take(_recentAppsExpanded
                     ? ExpandedRecentAppCount
                     : CollapsedRecentAppCount))
        {
            RecentApps.Add(app);
        }

        RecentExpandText.Text = _recentAppsExpanded ? "折叠" : "展开";
        RecentExpandGlyph.Text = _recentAppsExpanded ? "\uE70E" : "\uE70D";
    }

    private void AppButton_Click(object sender, RoutedEventArgs e)
    {
        if (Environment.TickCount64 < _suppressTileActivationUntil)
        {
            return;
        }

        if (sender is not Button { Tag: AppEntry app })
        {
            return;
        }

        if (app.IsFolder)
        {
            _ = ToggleAppFolderAsync(app);
            return;
        }

        if (AppLauncher.Launch(app))
        {
            DismissWindow(yieldTopmost: true);
        }
    }

    private bool AddAppTile(TileGroup target, AppEntry app, System.Windows.Point position)
    {
        var tile = CreateAppTile(app);
        var (column, row) = TileDropResolver.GetCell(position, _appDragAnchor, tile, target.ContentColumns);
        if (!Win10GroupLayout.Add(target, tile, column, row))
        {
            return false;
        }

        TileLayoutStore.Save(TileLayout);
        return true;
    }

    private TileItem CreateAppTile(AppEntry app)
    {
        var tile = new TileItem
        {
            Name = app.Name,
            LaunchTarget = app.LaunchTarget,
            TargetType = TileTargetType.Application,
            Size = TileSize.Medium,
            Icon = app.Icon,
        };
        RestoreTileIcon(tile, _launchableApps);
        return tile;
    }

    private static void RestoreTileIcons(TileLayout layout, IReadOnlyList<AppEntry> apps)
    {
        foreach (var tile in layout.Groups.SelectMany(group => group.Tiles))
        {
            RestoreTileIconTree(tile, apps);
        }
    }

    private static void RestoreTileIconTree(TileItem tile, IReadOnlyList<AppEntry> apps)
    {
        tile.BackgroundImage = ShellIconLoader.LoadImage(tile.BackgroundImagePath);
        RestoreTileIcon(tile, apps);
        foreach (var child in tile.FolderTiles)
        {
            RestoreTileIconTree(child, apps);
        }
    }

    private static void RestoreTileIcon(TileItem tile, IReadOnlyList<AppEntry> apps)
    {
        tile.UsesFullTileLogo = false;
        if (!string.IsNullOrWhiteSpace(tile.IconPath))
        {
            tile.Icon = ShellIconLoader.Load(tile.IconPath) ?? GenericAppIcon.Image;
            return;
        }

        var app = apps.FirstOrDefault(candidate =>
            candidate.LaunchTarget.Equals(tile.LaunchTarget, StringComparison.OrdinalIgnoreCase));
        if (app is not null)
        {
            var tileLogo = tile.IconPosition == TileIconPosition.Center && tile.IconSize == 32
                ? PackagedTileAssetLoader.Load(app.PackageInstallPath, app.AppUserModelId, tile.Size)
                : null;
            if (tileLogo is not null)
            {
                tile.Icon = tileLogo;
                tile.UsesFullTileLogo = true;
                return;
            }

            tile.Icon = app.Icon ?? ShellIconLoader.Load(tile.LaunchTarget) ?? GenericAppIcon.Image;
            return;
        }

        tile.Icon = ShellIconLoader.Load(tile.LaunchTarget) ?? GenericAppIcon.Image;
    }
}
