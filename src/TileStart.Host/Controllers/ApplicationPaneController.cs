using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using TileStart.Host.Applications;
using TileStart.Host.Icons;
using TileStart.Host.Shell;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Tiles.Layout;
using TileStart.Host.Tiles.DragDrop;
using TileStart.Host.Persistence;
using TileStart.Host.Utilities;

namespace TileStart.Host.Controllers;

internal sealed class ApplicationPaneController : IDisposable
{
    internal const int CollapsedRecentAppCount = 3;
    private const int ExpandedRecentAppCount = 10;
    private static readonly TimeSpan ApplicationRefreshDebounce = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PackagedAppRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly RangeObservableCollection<AppEntry> _apps = [];
    private readonly RangeObservableCollection<IApplicationListItem> _applicationListItems = [];
    private readonly RecentApplicationsSection _recentSection = new();
    private readonly Queue<HostRequest> _pendingHostRequests = [];
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly object _applicationRefreshScheduleLock = new();
    private readonly List<FileSystemWatcher> _startMenuWatchers = [];
    private System.Windows.Threading.DispatcherOperation? _contextMenuPrewarmOperation;
    private CancellationTokenSource? _applicationRefreshDebounceCancellation;
    private Task? _packagedAppRefreshTask;
    private AppEntry[] _launchableApps = [];
    private AppEntry[] _recentAppCandidates = [];
    private bool _applicationContentReady;
    private bool _applicationRefreshInProgress;
    private bool _applicationRefreshPending;
    private bool _recentAppsExpanded;
    private bool _layoutRestored;
    private bool _isDisposed;

    private readonly TileLayout _tileLayout;
    private readonly TileLayout? _savedLayout;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly Button _navigationToggleButton;
    private readonly Grid _windowRoot;
    private readonly Action _showFromShell;
    private readonly Action<bool> _dismissWindow;
    private readonly Func<AppEntry, Task> _toggleAppFolderAsync;
    private readonly Func<TileItem, bool> _pinTileToStart;
    private readonly Func<bool> _ensureGroupGridCoordinates;
    private readonly Action _prepareMotionElements;
    private readonly Action _updateLayout;

    public ApplicationPaneController(
        TileLayout tileLayout,
        TileLayout? savedLayout,
        System.Windows.Threading.Dispatcher dispatcher,
        Button navigationToggleButton,
        Grid windowRoot,
        Action showFromShell,
        Action<bool> dismissWindow,
        Func<AppEntry, Task> toggleAppFolderAsync,
        Func<TileItem, bool> pinTileToStart,
        Func<bool> ensureGroupGridCoordinates,
        Action prepareMotionElements,
        Action updateLayout)
    {
        _lifetimeToken = _lifetimeCancellation.Token;
        _tileLayout = tileLayout;
        _savedLayout = savedLayout;
        _dispatcher = dispatcher;
        _navigationToggleButton = navigationToggleButton;
        _windowRoot = windowRoot;
        _showFromShell = showFromShell;
        _dismissWindow = dismissWindow;
        _toggleAppFolderAsync = toggleAppFolderAsync;
        _pinTileToStart = pinTileToStart;
        _ensureGroupGridCoordinates = ensureGroupGridCoordinates;
        _prepareMotionElements = prepareMotionElements;
        _updateLayout = updateLayout;

        _applicationListItems.Add(_recentSection);
        AppsView = new ListCollectionView(_applicationListItems);
        AppsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(IApplicationListItem.SortLetter)));
        AppsView.SortDescriptions.Add(
            new SortDescription(nameof(IApplicationListItem.SortLetter), ListSortDirection.Ascending));
        AppsView.SortDescriptions.Add(new SortDescription(nameof(IApplicationListItem.SortName),
            ListSortDirection.Ascending));
    }

    public ObservableCollection<AppEntry> RecentApps => _recentSection.Apps;

    public RecentApplicationsSection RecentSection => _recentSection;

    public string CurrentUserName { get; } = Environment.UserName;

    public ImageSource? CurrentUserPicture { get; } = UserAccountPictureLoader.Load();

    public ICollectionView AppsView { get; }

    public IReadOnlyList<AlphabetIndexEntry> AlphabetLetters { get; } = AlphabetIndex.Create();

    public IReadOnlyList<AppEntry> LaunchableApps => _launchableApps;

    public bool ApplicationContentReady => _applicationContentReady;

    public bool RecentAppsExpanded => _recentAppsExpanded;

    public IList<AppEntry> AllApps => _apps;

    public void RestoreSavedLayout()
    {
        if (_layoutRestored)
        {
            return;
        }

        _layoutRestored = true;
        var layout = _savedLayout ?? new TileLayout();
        foreach (var group in layout.Groups)
        {
            _tileLayout.Groups.Add(group);
        }

        _updateLayout();
        var migratedGroupCoordinates = _ensureGroupGridCoordinates();
        if (_savedLayout is null || migratedGroupCoordinates)
        {
            TileLayoutStore.Save(_tileLayout);
        }

        _prepareMotionElements();
        DiagnosticLog.Write($"Tile layout ready: {_tileLayout.Groups.Sum(group => group.Tiles.Count)} tiles.");
    }

    public async Task LoadAppsAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var apps = await ScanApplicationsAsync();
            _lifetimeToken.ThrowIfCancellationRequested();
            _apps.AddRange(apps);
            _applicationListItems.AddRange(apps);

            var launchableApps = AppEntry.FlattenApplications(apps).ToArray();
            _launchableApps = launchableApps;
            _recentAppCandidates = launchableApps
                .Where(app => app.AddedAt > DateTime.MinValue)
                .OrderByDescending(app => app.AddedAt)
                .Take(ExpandedRecentAppCount)
                .ToArray();
            RefreshRecentApps();

            AlphabetIndex.UpdateAvailability(AlphabetLetters, apps, RecentApps.Count > 0);
            RestoreSavedLayout();
            _applicationContentReady = true;
            DiagnosticLog.Write("Application content ready.");
            QueueContextMenuPrewarm();
            _ = LoadTileVisualsAsync(launchableApps);
            _ = LoadApplicationIconsAsync(launchableApps);
            StartApplicationChangeMonitoring();
            while (_pendingHostRequests.Count > 0)
            {
                HandleHostRequest(_pendingHostRequests.Dequeue());
            }
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application list load failed: {exception}");
        }
    }

    public async Task RefreshAppsAsync()
    {
        if (_isDisposed || !_applicationContentReady)
        {
            return;
        }

        if (_applicationRefreshInProgress)
        {
            _applicationRefreshPending = true;
            return;
        }

        _applicationRefreshInProgress = true;
        _applicationRefreshPending = false;
        try
        {
            var apps = await ScanApplicationsAsync();
            _lifetimeToken.ThrowIfCancellationRequested();
            if (ApplicationTreesMatch(_apps, apps))
            {
                return;
            }

            // 扫描会生成新的 AppEntry。复用旧图标可避免应用列表在后台刷新完成时
            // 先退回占位图，再等待相同应用重新解码图标而产生闪烁。
            ReuseLoadedIcons(_launchableApps, apps);
            _apps.Clear();
            _apps.AddRange(apps);
            RefreshApplicationCollection();
            DiagnosticLog.Write($"Application list refreshed: {_launchableApps.Length} launchable apps.");
            _ = LoadApplicationIconsAsync(_launchableApps);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application list refresh failed: {exception}");
        }
        finally
        {
            _applicationRefreshInProgress = false;
            if (_applicationRefreshPending)
            {
                QueueApplicationRefresh();
            }
        }
    }

    private void StartApplicationChangeMonitoring()
    {
        foreach (var directory in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                 }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName
                                   | NotifyFilters.CreationTime
                                   | NotifyFilters.LastWrite,
                };
                watcher.Changed += StartMenuWatcher_Changed;
                watcher.Created += StartMenuWatcher_Changed;
                watcher.Deleted += StartMenuWatcher_Changed;
                watcher.Renamed += StartMenuWatcher_Changed;
                watcher.Error += StartMenuWatcher_Error;
                watcher.EnableRaisingEvents = true;
                _startMenuWatchers.Add(watcher);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DiagnosticLog.Write($"Start menu watcher unavailable for '{directory}': {exception.Message}");
            }
        }

        // shell:AppsFolder 没有与传统开始菜单目录等价的文件事件。低频后台兜底
        // 用于发现纯 MSIX/UWP 安装变化，但绝不再由菜单显示路径触发。
        _packagedAppRefreshTask = MonitorPackagedAppsAsync();
    }

    private void StartMenuWatcher_Changed(object sender, FileSystemEventArgs e) => QueueApplicationRefresh();

    private void StartMenuWatcher_Error(object sender, ErrorEventArgs e)
    {
        DiagnosticLog.Write($"Start menu watcher error: {e.GetException()?.Message}");
        QueueApplicationRefresh();
    }

    private void QueueApplicationRefresh()
    {
        CancellationTokenSource request;
        lock (_applicationRefreshScheduleLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _applicationRefreshDebounceCancellation?.Cancel();
            _applicationRefreshDebounceCancellation?.Dispose();
            request = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
            _applicationRefreshDebounceCancellation = request;
        }

        _ = RefreshApplicationsAfterDebounceAsync(request);
    }

    private async Task RefreshApplicationsAfterDebounceAsync(CancellationTokenSource request)
    {
        try
        {
            await Task.Delay(ApplicationRefreshDebounce, request.Token);
            var operation = _dispatcher.InvokeAsync(
                RefreshAppsAsync,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                request.Token);
            await operation.Task.Unwrap();
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_applicationRefreshScheduleLock)
            {
                if (ReferenceEquals(_applicationRefreshDebounceCancellation, request))
                {
                    _applicationRefreshDebounceCancellation = null;
                }
            }

            request.Dispose();
        }
    }

    private async Task MonitorPackagedAppsAsync()
    {
        using var timer = new PeriodicTimer(PackagedAppRefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_lifetimeToken))
            {
                QueueApplicationRefresh();
            }
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<List<AppEntry>> ScanApplicationsAsync()
    {
        var scannedApps = FilterHiddenApplications(
            await StartAppScanner.ScanAsync(),
            AppVisibilityStore.Load());
        var customApps = CustomAppStore.Load();
        var scannedLaunchableApps = AppEntry.FlattenApplications(scannedApps).ToArray();
        var apps = customApps.Count == 0
            ? scannedApps.ToList()
            : scannedApps
                .Concat(customApps.Where(custom => !scannedLaunchableApps.Any(existing =>
                    LaunchTargetIdentity.GetKey(existing.LaunchTarget)
                    == LaunchTargetIdentity.GetKey(custom.LaunchTarget))))
                .ToList();
        RemoveMissingApplications(apps);
        return apps;
    }

    internal static bool ApplicationTreesMatch(
        IReadOnlyList<AppEntry> current,
        IReadOnlyList<AppEntry> scanned)
    {
        if (current.Count != scanned.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            var left = current[index];
            var right = scanned[index];
            if (left.IsFolder != right.IsFolder
                || !left.Name.Equals(right.Name, StringComparison.CurrentCulture)
                || left.AddedAt != right.AddedAt
                || !left.PackageInstallPath.Equals(right.PackageInstallPath, StringComparison.OrdinalIgnoreCase)
                || !left.AppUserModelId.Equals(right.AppUserModelId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (left.IsFolder)
            {
                if (!ApplicationTreesMatch(left.Children, right.Children))
                {
                    return false;
                }
            }
            else if (LaunchTargetIdentity.GetKey(left.LaunchTarget)
                     != LaunchTargetIdentity.GetKey(right.LaunchTarget))
            {
                return false;
            }
        }

        return true;
    }

    private static void ReuseLoadedIcons(IEnumerable<AppEntry> current, IEnumerable<AppEntry> scanned)
    {
        var icons = AppEntry.FlattenApplications(current)
            .Where(app => app.Icon is not null)
            .GroupBy(app => LaunchTargetIdentity.GetKey(app.LaunchTarget), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Icon!, StringComparer.OrdinalIgnoreCase);
        foreach (var app in AppEntry.FlattenApplications(scanned))
        {
            if (icons.TryGetValue(LaunchTargetIdentity.GetKey(app.LaunchTarget), out var icon))
            {
                app.Icon = icon;
            }
        }
    }

    public void HandleHostRequest(HostRequest request)
    {
        if (_isDisposed)
        {
            return;
        }

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
        _applicationListItems.Add(app);
        RefreshApplicationCollection();
        _ = LoadApplicationIconsAsync([app]);
        ShowIfHidden();
    }

    private void PinExternalTile(string path)
    {
        var tile = DroppedTileFactory.Create(path);
        if (tile is null)
        {
            return;
        }

        var identity = LaunchTargetIdentity.GetKey(tile.LaunchTarget);
        if (tile.TargetType == TileTargetType.Application && !CustomAppStore.Contains(path))
        {
            AppVisibilityStore.Hide(identity);
            RemoveApplicationFromList(identity);
        }

        if (_tileLayout.Groups.SelectMany(group => group.Tiles)
            .Any(existing => LaunchTargetIdentity.GetKey(existing.LaunchTarget) == identity))
        {
            ShowIfHidden();
            return;
        }

        if (_pinTileToStart(tile))
        {
            ShowIfHidden();
        }
    }

    public void ShowIfHidden()
    {
        _showFromShell();
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

    public void RemoveApplicationFromList(string identity)
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

    public static bool RemoveMissingApplications(IList<AppEntry> entries)
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

    public static bool IsMissingFileApplication(AppEntry app)
    {
        if (app.LaunchTarget.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return Path.IsPathFullyQualified(app.LaunchTarget)
                   && !File.Exists(app.LaunchTarget)
                   && !Directory.Exists(app.LaunchTarget);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            return true;
        }
    }

    public void RefreshApplicationCollection()
    {
        _applicationListItems.Clear();
        _applicationListItems.Add(_recentSection);
        _applicationListItems.AddRange(_apps);
        _launchableApps = [.. AppEntry.FlattenApplications(_apps)];
        _recentAppCandidates = _launchableApps
            .Where(app => app.AddedAt > DateTime.MinValue)
            .OrderByDescending(app => app.AddedAt)
            .Take(ExpandedRecentAppCount)
            .ToArray();
        RefreshRecentApps();
        AppsView.Refresh();
        AlphabetIndex.UpdateAvailability(AlphabetLetters, _apps, RecentApps.Count > 0);
    }

    private void QueueContextMenuPrewarm()
    {
        if (_isDisposed)
        {
            return;
        }

        _contextMenuPrewarmOperation = _dispatcher.BeginInvoke(
            PrewarmContextMenus,
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void PrewarmContextMenus()
    {
        _contextMenuPrewarmOperation = null;
        if (_isDisposed)
        {
            return;
        }

        var startedAt = Environment.TickCount64;
        var owners = new List<Button> { _navigationToggleButton };
        var appOwner = FindVisualDescendants<Button>(_windowRoot)
            .FirstOrDefault(button => button.ContextMenu is not null && button.Tag is AppEntry { IsFolder: false });
        var tileOwner = FindVisualDescendants<Button>(_windowRoot)
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
        if (_isDisposed)
        {
            return;
        }

        try
        {
            var classicApps = apps.Where(app => string.IsNullOrWhiteSpace(app.AppUserModelId)).ToArray();
            var packagedApps = apps.Where(app => !string.IsNullOrWhiteSpace(app.AppUserModelId)).ToArray();
            var loadedGroups = await Task.WhenAll(
                RunBackgroundThreadAsync(() => LoadApplicationIcons(classicApps), "TileStart Classic Icon Loader"),
                RunStaThreadAsync(() => LoadApplicationIcons(packagedApps), "TileStart Packaged Icon Loader"));
            var loadedIcons = loadedGroups.SelectMany(group => group).ToArray();
            _lifetimeToken.ThrowIfCancellationRequested();
            await _dispatcher.InvokeAsync(
                () => ApplyApplicationIcons(loadedIcons),
                System.Windows.Threading.DispatcherPriority.Background,
                _lifetimeToken);
            DiagnosticLog.Write($"Application icon loading completed: {apps.Count} entries processed.");
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application icon load failed: {exception}");
        }
    }

    private static IReadOnlyList<LoadedApplicationIcon> LoadApplicationIcons(IEnumerable<AppEntry> apps)
    {
        var loadedIcons = new List<LoadedApplicationIcon>();
        foreach (var app in apps)
        {
            try
            {
                // Packaged apps declare a Square44x44Logo specifically for shell app lists.
                // Prefer that deterministic asset over the Shell thumbnail API, which can
                // return a large bitmap containing only a tiny glyph on some Windows builds.
                var icon = PackagedTileAssetLoader.LoadApplicationIcon(app.PackageInstallPath, app.AppUserModelId)
                           ?? ShellIconLoader.Load(app.LaunchTarget)
                           ?? GenericAppIcon.Image;
                loadedIcons.Add(new LoadedApplicationIcon(app, icon));
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Application icon load failed for '{app.LaunchTarget}': {exception.Message}");
            }
        }

        return loadedIcons;
    }

    private void ApplyApplicationIcons(IReadOnlyList<LoadedApplicationIcon> loadedIcons)
    {
        if (_dispatcher.HasShutdownStarted)
        {
            return;
        }

        var iconsByTarget = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var loaded in loadedIcons)
        {
            loaded.App.Icon = loaded.Icon;
            iconsByTarget[loaded.App.LaunchTarget] = loaded.Icon;
        }

        foreach (var tile in _tileLayout.Groups.SelectMany(group => group.Tiles))
        {
            ApplyApplicationIconsToTile(tile, iconsByTarget);
        }
    }

    private static void ApplyApplicationIconsToTile(
        TileItem tile,
        IReadOnlyDictionary<string, ImageSource> iconsByTarget)
    {
        if (string.IsNullOrWhiteSpace(tile.IconPath) &&
            !tile.UsesFullTileLogo &&
            iconsByTarget.TryGetValue(tile.LaunchTarget, out var icon))
        {
            tile.Icon = icon;
        }

        foreach (var child in tile.FolderTiles)
        {
            ApplyApplicationIconsToTile(child, iconsByTarget);
        }
    }

    private static Task<T> RunBackgroundThreadAsync<T>(Func<T> action, string name)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = name,
            Priority = ThreadPriority.Lowest,
        };
        thread.Start();
        return completion.Task;
    }

    private static Task<T> RunStaThreadAsync<T>(Func<T> action, string name)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = name,
            Priority = ThreadPriority.Lowest,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private readonly record struct LoadedApplicationIcon(AppEntry App, ImageSource Icon);

    public void ToggleRecentApps()
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

        _recentSection.Update(
            _recentAppsExpanded,
            _recentAppCandidates.Length > CollapsedRecentAppCount);
    }

    public async Task AppButtonClick(AppEntry app)
    {
        if (app.IsFolder)
        {
            await _toggleAppFolderAsync(app);
            return;
        }

        if (AppLauncher.Launch(app))
        {
            _dismissWindow(true);
        }
    }

    public bool AddAppTile(TileGroup target, AppEntry app, System.Windows.Point position,
        System.Windows.Point dragAnchor)
    {
        return AddAppTile(target, CreateAppTile(app), position, dragAnchor);
    }

    public bool AddAppTile(TileGroup target, TileItem tile, System.Windows.Point position,
        System.Windows.Point dragAnchor)
    {
        if (_tileLayout.ContainsLaunchTarget(tile.LaunchTarget))
        {
            return false;
        }

        var (column, row) = TileDropResolver.GetCell(position, dragAnchor, tile, target.ContentColumns);
        if (!Win10GroupLayout.Add(target, tile, column, row))
        {
            return false;
        }

        _updateLayout();
        TileLayoutStore.Save(_tileLayout);
        return true;
    }

    public TileItem CreateAppTile(AppEntry app)
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

    private async Task LoadTileVisualsAsync(IReadOnlyList<AppEntry> apps)
    {
        try
        {
            var loadedVisuals = await RunStaThreadAsync(
                () => LoadTileVisuals(_tileLayout, apps),
                "TileStart Tile Visual Loader");
            _lifetimeToken.ThrowIfCancellationRequested();
            await _dispatcher.InvokeAsync(
                () => ApplyTileVisuals(loadedVisuals),
                System.Windows.Threading.DispatcherPriority.Background,
                _lifetimeToken);
            DiagnosticLog.Write($"Tile visual loading completed: {loadedVisuals.Count} tiles processed.");
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Tile visual load failed: {exception}");
        }
    }

    private static IReadOnlyList<LoadedTileVisual> LoadTileVisuals(
        TileLayout layout,
        IReadOnlyList<AppEntry> apps)
    {
        var loadedVisuals = new List<LoadedTileVisual>();
        foreach (var tile in layout.Groups.SelectMany(group => group.Tiles))
        {
            LoadTileVisualTree(tile, apps, loadedVisuals);
        }

        return loadedVisuals;
    }

    private static void LoadTileVisualTree(
        TileItem tile,
        IReadOnlyList<AppEntry> apps,
        ICollection<LoadedTileVisual> loadedVisuals)
    {
        var (icon, usesFullTileLogo) = LoadTileIcon(tile, apps);
        loadedVisuals.Add(new LoadedTileVisual(
            tile,
            ShellIconLoader.LoadImage(tile.BackgroundImagePath),
            icon,
            usesFullTileLogo));
        foreach (var child in tile.FolderTiles)
        {
            LoadTileVisualTree(child, apps, loadedVisuals);
        }
    }

    private static void ApplyTileVisuals(IReadOnlyList<LoadedTileVisual> loadedVisuals)
    {
        foreach (var loaded in loadedVisuals)
        {
            loaded.Tile.BackgroundImage = loaded.BackgroundImage;
            loaded.Tile.Icon = loaded.Icon;
            loaded.Tile.UsesFullTileLogo = loaded.UsesFullTileLogo;
        }
    }

    public static void RestoreTileIcons(TileLayout layout, IReadOnlyList<AppEntry> apps)
    {
        ApplyTileVisuals(LoadTileVisuals(layout, apps));
    }

    public static void RestoreTileIcon(TileItem tile, IReadOnlyList<AppEntry> apps)
    {
        var (icon, usesFullTileLogo) = LoadTileIcon(tile, apps);
        tile.Icon = icon;
        tile.UsesFullTileLogo = usesFullTileLogo;
    }

    private static (ImageSource Icon, bool UsesFullTileLogo) LoadTileIcon(
        TileItem tile,
        IReadOnlyList<AppEntry> apps)
    {
        if (!string.IsNullOrWhiteSpace(tile.IconPath))
        {
            return (ShellIconLoader.Load(tile.IconPath) ?? ResolveFallbackIcon(tile), false);
        }

        var tileTargetKey = LaunchTargetIdentity.GetKey(tile.LaunchTarget);
        var app = apps.FirstOrDefault(candidate =>
            LaunchTargetIdentity.GetKey(candidate.LaunchTarget).Equals(
                tileTargetKey,
                StringComparison.OrdinalIgnoreCase));
        var usesDefaultPackagedAppearance = UsesDefaultPackagedTileAppearance(tile);
        if (app is not null)
        {
            var tileLogo = usesDefaultPackagedAppearance
                ? PackagedTileAssetLoader.Load(app.PackageInstallPath, app.AppUserModelId, tile.Size)
                : null;
            if (tileLogo is not null)
            {
                return (tileLogo, true);
            }

            return (app.Icon ?? ShellIconLoader.Load(tile.LaunchTarget) ?? ResolveFallbackIcon(tile), false);
        }

        // Chromium Edge 在 AppsFolder 中以 MSEdge shell alias 暴露，PackageInstallPath/PFN 为空，
        // 但原生开始菜单仍使用其 Appx Square150x150Logo。只在默认外观下补这条系统资产链，
        // 避免覆盖用户主动设置的图标路径、大小或位置。
        var shellAliasLogo = usesDefaultPackagedAppearance
            ? PackagedTileAssetLoader.LoadKnownShellAlias(tile.LaunchTarget, tile.Size)
            : null;
        if (shellAliasLogo is not null)
        {
            return (shellAliasLogo, true);
        }

        return (ShellIconLoader.Load(tile.LaunchTarget) ?? ResolveFallbackIcon(tile), false);
    }

    internal static ImageSource ResolveFallbackIcon(TileItem tile) =>
        tile.TargetType == TileTargetType.Command ? TileStartAppIcon.Image : GenericAppIcon.Image;

    internal static bool UsesDefaultPackagedTileAppearance(TileItem tile) =>
        string.IsNullOrWhiteSpace(tile.IconPath)
        && tile.IconPosition == TileIconPosition.Center
        && Math.Abs(tile.IconSize - 32) < 0.01;

    private readonly record struct LoadedTileVisual(
        TileItem Tile,
        ImageSource? BackgroundImage,
        ImageSource Icon,
        bool UsesFullTileLogo);

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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCancellation.Cancel();
        lock (_applicationRefreshScheduleLock)
        {
            _applicationRefreshDebounceCancellation?.Cancel();
            _applicationRefreshDebounceCancellation?.Dispose();
            _applicationRefreshDebounceCancellation = null;
        }

        foreach (var watcher in _startMenuWatchers)
        {
            watcher.Dispose();
        }

        _startMenuWatchers.Clear();
        _packagedAppRefreshTask = null;
        _contextMenuPrewarmOperation?.Abort();
        _contextMenuPrewarmOperation = null;
        _pendingHostRequests.Clear();
        _lifetimeCancellation.Dispose();
    }
}