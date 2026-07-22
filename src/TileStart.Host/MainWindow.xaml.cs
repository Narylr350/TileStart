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

namespace TileStart.Host;

public partial class MainWindow : Window
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint SwpNoActivate = 0x0010;
    private const uint AwHide = 0x00010000;
    private const uint AwBlend = 0x00080000;
    private const int DismissDurationMilliseconds = 150;
    private const int ForegroundAcquisitionTimeoutMilliseconds = 1000;
    private const int CollapsedRecentAppCount = 3;
    private const int ExpandedRecentAppCount = 10;
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int WmActivate = 0x0006;
    private const int WaInactive = 0;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopRight = 14;
    private const uint GwOwner = 4;
    private static readonly nint HwndTopmost = new(-1);
    private readonly RangeObservableCollection<AppEntry> _apps = [];
    private AppEntry[] _launchableApps = [];
    private AppEntry[] _recentAppCandidates = [];
    private bool _allowClose;
    private bool _recentAppsExpanded;
    private bool _isDismissing;
    private HwndSource? _windowSource;
    private bool _foregroundAcquiredSinceShow;
    private long _foregroundAcquisitionDeadline;
    private int _openContextMenuCount;
    private TaskbarEdge _taskbarEdge = TaskbarEdge.Bottom;
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

    private readonly System.Windows.Threading.DispatcherTimer _foregroundWatchdogTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(50),
    };

    private readonly TileReflowStability _tileReflowStability = new();
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
    private bool _entranceSnapshotActive;
    private int _entranceSnapshotGeneration;
    private long _suppressTileActivationUntil;
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
    private bool _navigationExpanded;
    private bool _navigationPinnedOpen;
    private readonly NavigationPreferences _navigationPreferences = NavigationPreferencesStore.Load();
    private bool _isWindowWidthSnapAnimating;
    private long _windowWidthSnapStartedAt;
    private double _windowWidthSnapFrom;
    private double _windowWidthSnapTo;
    private double _windowWidthSnapRight;
    private Dictionary<TileGroup, System.Windows.Point>? _windowWidthSnapGroupPositions;
    private int _semanticZoomAnimationGeneration;
    private bool _isLetterIndexActive;
    private bool _isSemanticZoomAnimating;
    private int _appFolderAnimationGeneration;
    private int _tileFolderAnimationGeneration;
    private bool _isAppFolderAnimating;
    private bool _isTileFolderAnimating;
#if DEBUG
    private string? _tileDropTraceCandidateKey;
    private System.Windows.Point _tileDropTraceCandidatePosition;
    private string? _tileDropGeometryTraceSignature;
#endif

    public MainWindow()
    {
        AppsView = new ListCollectionView(_apps);
        AppsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppEntry.SortLetter)));
        AppsView.SortDescriptions.Add(new SortDescription(nameof(AppEntry.SortLetter), ListSortDirection.Ascending));
        AppsView.SortDescriptions.Add(new SortDescription(nameof(AppEntry.Name), ListSortDirection.Ascending));
        InitializeComponent();
        ApplyNavigationPreferences();
        SemanticZoomViewport.SizeChanged += SemanticZoomViewport_SizeChanged;
        _tileReflowTimer.Tick += TileReflowTimer_Tick;
        _folderActivationTimer.Tick += FolderActivationTimer_Tick;
        _foregroundWatchdogTimer.Tick += ForegroundWatchdogTimer_Tick;
        DataContext = this;
        MinWidth = StartWindowSizing.WidthForColumns(StartWindowSizing.MinimumGroupColumns);
        MaxWidth = StartWindowSizing.WidthForColumns(StartWindowSizing.MaximumGroupColumns);
        var savedSize = WindowSizeStore.Load();
        if (savedSize is not null)
        {
            Width = StartWindowSizing.SnapWidth(savedSize.Value.Width, MaxWidth);
            Height = Math.Max(MinHeight, savedSize.Value.Height);
        }
        else
        {
            Width = StartWindowSizing.WidthForColumns(2);
        }

        _ = LoadAppsAsync();
    }

    public ObservableCollection<AppEntry> RecentApps { get; } = [];

    public string CurrentUserName { get; } = Environment.UserName;

    public ImageSource? CurrentUserPicture { get; } = UserAccountPictureLoader.Load();

    public ICollectionView AppsView { get; }

    public IReadOnlyList<AlphabetIndexEntry> AlphabetLetters { get; } = AlphabetIndex.Create();

    public TileLayout TileLayout { get; } = new();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowSource = PresentationSource.FromVisual(this) as HwndSource;
        _windowSource?.AddHook(WindowMessageHook);
        ApplyWindowMaterial();
    }

    public void ShowFromShell()
    {
        if (IsVisible)
        {
            DismissWindow();
            return;
        }

        CancelEntranceSnapshot();
        ApplyWindowMaterial();
        _foregroundAcquiredSinceShow = false;
        _foregroundAcquisitionDeadline = Environment.TickCount64 + ForegroundAcquisitionTimeoutMilliseconds;
        PositionOnCurrentMonitor();
        PrepareMotionElements();
        var animationsEnabled = SystemParameters.ClientAreaAnimation;
        var usesSnapshot = animationsEnabled && TryPrepareEntranceSnapshot();
        FrameworkElement motionRoot = usesSnapshot ? WindowRoot : MainSurface;
        FrameworkElement[] motionElements = [usesSnapshot ? EntrancePreview : MainSurface];
        StartMotion.StageEntrance(motionRoot, motionElements, _taskbarEdge == TaskbarEdge.Bottom, animationsEnabled);
        Show();
        UpdateLayout();
        EnsureTileScrollBarClearance();
        PositionOnCurrentMonitor();
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            () =>
            {
                if (EnsureTileScrollBarClearance())
                {
                    PositionOnCurrentMonitor();
                }
            });
        Activate();
        Focus();
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != 0)
        {
            SetForegroundWindow(handle);
        }

        var foreground = GetForegroundWindow();
        _foregroundAcquiredSinceShow = StartWindowLifecycle.HasAcquiredForeground(
            _foregroundAcquiredSinceShow,
            foreground != 0 && ForegroundBelongsToStart(foreground),
            receivedNativeActivation: false);

        _foregroundWatchdogTimer.Start();
        var generation = _entranceSnapshotGeneration;
        StartMotion.PlayEntrance(
            motionElements,
            animationsEnabled,
            usesSnapshot ? () => CompleteEntranceSnapshot(generation) : null);
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveCurrentSize();
        if (!_allowClose)
        {
            e.Cancel = true;
            _foregroundWatchdogTimer.Stop();
            Hide();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopWindowWidthSnapAnimation();
        StopTileDragAutoScroll();
        _foregroundWatchdogTimer.Stop();
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        base.OnClosed(e);
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _groupDragTransaction is not null)
        {
            FinishGroupDrag(commit: false);
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private async Task LoadAppsAsync()
    {
        try
        {
            var apps = await StartAppScanner.ScanAsync();
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
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application list load failed: {exception}");
        }
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
                var icon = ShellIconLoader.Load(app.LaunchTarget);
                if (icon is null)
                {
                    continue;
                }

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

    private void PrepareMotionElements()
    {
        if (IsVisible)
        {
            return;
        }

        var size = new System.Windows.Size(Math.Max(MinWidth, Width), Math.Max(MinHeight, Height));
        WindowRoot.Measure(size);
        WindowRoot.Arrange(new System.Windows.Rect(new System.Windows.Point(), size));
        WindowRoot.UpdateLayout();
        StartMotion.Prepare([MainSurface, EntrancePreview]);
    }

    private bool TryPrepareEntranceSnapshot()
    {
        if (MainSurface.ActualWidth <= 0 || MainSurface.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            EntrancePreview.Source = CaptureElement(MainSurface);
            EntrancePreview.Width = MainSurface.ActualWidth;
            EntrancePreview.Height = MainSurface.ActualHeight;
            EntrancePreview.Visibility = Visibility.Visible;
            MainSurface.Visibility = Visibility.Hidden;
            _entranceSnapshotActive = true;
            _entranceSnapshotGeneration++;
            WindowRoot.UpdateLayout();
            return true;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Entrance snapshot failed: {exception}");
            CancelEntranceSnapshot();
            return false;
        }
    }

    private void CompleteEntranceSnapshot(int generation)
    {
        if (!_entranceSnapshotActive || generation != _entranceSnapshotGeneration)
        {
            return;
        }

        MainSurface.Visibility = Visibility.Visible;
        EntrancePreview.Visibility = Visibility.Collapsed;
        EntrancePreview.Source = null;
        _entranceSnapshotActive = false;
    }

    private void CancelEntranceSnapshot()
    {
        _entranceSnapshotGeneration++;
        _entranceSnapshotActive = false;
        MainSurface.Visibility = Visibility.Visible;
        EntrancePreview.Visibility = Visibility.Collapsed;
        EntrancePreview.Source = null;
    }

    private void PositionOnCurrentMonitor()
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfoW(monitor, ref monitorInfo))
        {
            return;
        }

        var dpi = GetMonitorDpi(monitor);
        var monitorRect = ToPixelRect(monitorInfo.Monitor);
        var workArea = ToPixelRect(monitorInfo.WorkArea);
        var taskbarRect = FindTaskbarRect(monitor);
        var edge = StartWindowPlacement.InferTaskbarEdge(monitorRect, taskbarRect);
        _taskbarEdge = edge;
        var scale = 96.0 / dpi;
        var logicalWorkWidth = workArea.Width * scale;
        var logicalWorkHeight = workArea.Height * scale;
        MinWidth = Math.Min(StartWindowSizing.WidthForColumns(StartWindowSizing.MinimumGroupColumns), logicalWorkWidth);
        MaxWidth = StartWindowSizing.MaximumWidth(logicalWorkWidth);
        MaxHeight = Math.Max(MinHeight, logicalWorkHeight);
        var logicalWidth = StartWindowSizing.SnapWidth(ActualWidth > 0 ? ActualWidth : Width, logicalWorkWidth);
        var logicalHeight = StartWindowSizing.ClampHeight(
            ActualHeight > 0 ? ActualHeight : Height,
            MinHeight,
            logicalWorkHeight);
        var placement = StartWindowPlacement.Calculate(
            workArea,
            edge,
            (int)Math.Round(logicalWidth * dpi / 96.0),
            (int)Math.Round(logicalHeight * dpi / 96.0));
        Left = placement.Left * scale;
        Top = placement.Top * scale;
        Width = placement.Width * scale;
        Height = placement.Height * scale;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        var positioned = SetWindowPos(handle, HwndTopmost, placement.Left, placement.Top, placement.Width,
            placement.Height, SwpNoActivate);
        DiagnosticLog.Write(
            $"Window placement: monitor={monitorRect}, work={ToPixelRect(monitorInfo.WorkArea)}, taskbar={taskbarRect}, edge={edge}, target={placement}, positioned={positioned}, error={(positioned ? 0 : Marshal.GetLastWin32Error())}.");
    }

    private static PixelRect? FindTaskbarRect(nint monitor)
    {
        PixelRect? result = null;
        EnumWindows((window, _) =>
        {
            var className = new StringBuilder(64);
            GetClassNameW(window, className, className.Capacity);
            if (className.ToString() is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                || MonitorFromWindow(window, MonitorDefaultToNearest) != monitor
                || !GetWindowRect(window, out var rect))
            {
                return true;
            }

            result = ToPixelRect(rect);
            return false;
        }, 0);
        return result;
    }

    private static PixelRect ToPixelRect(Rect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static uint GetMonitorDpi(nint monitor)
    {
        return GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0 ? dpiX : 96;
    }

    private void ApplyWindowMaterial()
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var material = Win10Theme.ReadStartMaterial();
        var acrylicApplied = SetAccentPolicy(
            material.UseAcrylic ? AccentEnableAcrylicBlurBehind : 0,
            material.UseAcrylic ? 2 : 0,
            material.AcrylicGradientColor);
        MainSurface.Background = material.UseAcrylic && acrylicApplied
            ? System.Windows.Media.Brushes.Transparent
            : new SolidColorBrush(material.FallbackColor);
    }

    private bool SetAccentPolicy(int accentState, int accentFlags, int gradientColor)
    {
        var accent = new AccentPolicy
        {
            AccentState = accentState,
            AccentFlags = accentFlags,
            GradientColor = gradientColor,
        };
        var accentPointer = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try
        {
            Marshal.StructureToPtr(accent, accentPointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = accentPointer,
                SizeOfData = Marshal.SizeOf<AccentPolicy>(),
            };
            return SetWindowCompositionAttribute(new WindowInteropHelper(this).Handle, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPointer);
        }
    }

    private void SaveCurrentSize()
    {
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            WindowSizeStore.Save(ActualWidth, ActualHeight);
        }
    }

    private void DismissWindow(bool yieldTopmost = false)
    {
        if (!IsVisible)
        {
            return;
        }

        if (_isDismissing)
        {
            if (yieldTopmost)
            {
                Topmost = false;
            }

            return;
        }

        _isDismissing = true;
        _foregroundWatchdogTimer.Stop();
        CancelEntranceSnapshot();
        SaveCurrentSize();
        ClearSearch();
        var wasTopmost = Topmost;
        if (yieldTopmost)
        {
            Topmost = false;
        }

        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (SystemParameters.ClientAreaAnimation && handle != 0)
            {
                AnimateWindow(handle, DismissDurationMilliseconds, AwHide | AwBlend);
            }

            Hide();
        }
        finally
        {
            Topmost = wasTopmost;
            _isDismissing = false;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        RequestDismissAfterForegroundChange("deactivated");
    }

    private nint WindowMessageHook(
        nint window,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message != WmActivate)
        {
            return 0;
        }

        if ((wParam.ToInt64() & 0xffff) != WaInactive)
        {
            if (IsVisible)
            {
                _foregroundAcquiredSinceShow = StartWindowLifecycle.HasAcquiredForeground(
                    _foregroundAcquiredSinceShow,
                    foregroundBelongsToStart: false,
                    receivedNativeActivation: true);
            }

            return 0;
        }

        RequestDismissAfterForegroundChange("wm-activate");
        return 0;
    }

    private void RequestDismissAfterForegroundChange(string trigger)
    {
        Dispatcher.BeginInvoke(
            () => TryDismissAfterForegroundChange(trigger),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void StartContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _openContextMenuCount++;
        if (sender is not ContextMenu menu)
        {
            return;
        }

        if (GetContextMenuPopupBorder(menu) is { } border)
        {
            AnimateMenuPopupBorder(
                border,
                Win10MenuPopupMotion.TopLevelClosedRatio,
                ContextMenuOpensUpward(menu, border));
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
                else if (item.IsCheckable)
                {
                    item.IsChecked = TileContextActions.IsSelectedSize(tile.Size, item.Tag as string);
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

        AnimateMenuPopupBorder(
            border,
            Win10MenuPopupMotion.SubmenuClosedRatio,
            submenuOpensUpward);
    }

    private static void AnimateMenuPopupBorder(
        System.Windows.Controls.Border border,
        double closedRatio,
        bool popupOpensUpward)
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
            popupOpensUpward);
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

    private void ForegroundWatchdogTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            _foregroundWatchdogTimer.Stop();
            return;
        }

        if (!IsAnyMouseButtonPressed() && !_isInternalTileDrag && !_isInternalAppDrag && !_isInternalGroupDrag)
        {
            TryDismissAfterForegroundChange("foreground-watchdog");
        }
    }

    private void TryDismissAfterForegroundChange(string trigger)
    {
        if (!IsVisible
            || _isDismissing
            || IsAnyMouseButtonPressed()
            || _isInternalTileDrag
            || _isInternalAppDrag
            || _isInternalGroupDrag)
        {
            return;
        }

        var foreground = GetForegroundWindow();
        var foregroundKnown = foreground != 0;
        var foregroundBelongsToStart = foregroundKnown && ForegroundBelongsToStart(foreground);
        _foregroundAcquiredSinceShow = StartWindowLifecycle.HasAcquiredForeground(
            _foregroundAcquiredSinceShow,
            foregroundBelongsToStart,
            receivedNativeActivation: false);
        if (foregroundBelongsToStart)
        {
            return;
        }

        var hasActiveOwnedWindow = OwnedWindows.Cast<Window>().Any(window => window.IsActive);
        if (StartWindowLifecycle.ShouldHideForForegroundChange(
                _foregroundAcquiredSinceShow,
                foregroundKnown,
                foregroundBelongsToStart,
                hasActiveOwnedWindow,
                _openContextMenuCount > 0))
        {
            DiagnosticLog.Write(
                $"Window dismissal: trigger={trigger}, foreground=0x{foreground.ToInt64():X}, active={IsActive}, ownedActive={hasActiveOwnedWindow}, contextMenus={_openContextMenuCount}.");
            DismissWindow(yieldTopmost: true);
        }
        else if (!_foregroundAcquiredSinceShow
                 && foregroundKnown
                 && Environment.TickCount64 > _foregroundAcquisitionDeadline)
        {
            DiagnosticLog.Write(
                $"Window dismissal: trigger=foreground-acquisition-timeout, foreground=0x{foreground.ToInt64():X}, active={IsActive}.");
            DismissWindow(yieldTopmost: true);
        }
    }

    private bool ForegroundBelongsToStart(nint foreground)
    {
        var mainWindow = new WindowInteropHelper(this).Handle;
        if (foreground == 0 || mainWindow == 0)
        {
            return IsActive;
        }

        for (var window = foreground; window != 0; window = GetWindow(window, GwOwner))
        {
            if (window == mainWindow)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAnyMouseButtonPressed() =>
        Mouse.LeftButton == MouseButtonState.Pressed
        || Mouse.RightButton == MouseButtonState.Pressed
        || Mouse.MiddleButton == MouseButtonState.Pressed;

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ShowSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_isInternalAppDrag)
            {
                EndInternalAppDrag(commit: false, Mouse.GetPosition(MainSurface));
            }
            else if (_isInternalTileDrag)
            {
                EndInternalTileDrag(commit: false);
            }
            else if (LetterIndexPanel.Visibility == Visibility.Visible)
            {
                HideLetterIndex();
            }
            else if (SearchPanel.Visibility == Visibility.Visible)
            {
                ClearSearch();
            }
            else
            {
                DismissWindow();
            }

            e.Handled = true;
        }
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.OriginalSource == SearchBox || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        HideLetterIndex(animate: false);
        ShowSearch();
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        AppsView.Filter = item => item is AppEntry app && MatchesApp(app, query);
        if (query.Length > 0)
        {
            ExpandMatchingFolders(_apps, query);
        }

        RecentPanel.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppsView.Refresh();
    }

    private void ShowSearch()
    {
        HideLetterIndex(animate: false);
        SearchPanel.Visibility = Visibility.Visible;
        SearchBox.Focus();
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

    private void LetterHeader_Click(object sender, RoutedEventArgs e)
    {
        if (SearchPanel.Visibility == Visibility.Visible)
        {
            return;
        }

        if (_isLetterIndexActive)
        {
            return;
        }

        ResetSemanticZoomVisuals();
        _isLetterIndexActive = true;
        LetterIndexPanel.Visibility = Visibility.Visible;
        AppsScrollViewer.IsHitTestVisible = false;
        LetterIndexPanel.IsHitTestVisible = false;
        BeginSemanticZoomTransition(
            zoomedInViewActive: false,
            animate: true,
            () => LetterIndexPanel.IsHitTestVisible = true);
    }

    private void AlphabetLetter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AlphabetIndexEntry { IsAvailable: true } entry })
        {
            return;
        }

        if (entry.IsRecent)
        {
            AppsScrollViewer.ScrollToTop();
            RecentPanel.BringIntoView();
            HideLetterIndex();
            return;
        }

        var group = AppsView.Groups?
            .OfType<CollectionViewGroup>()
            .FirstOrDefault(candidate =>
                candidate.Name?.ToString()?.Equals(entry.TargetLetter, StringComparison.OrdinalIgnoreCase) == true);
        if (group is null)
        {
            return;
        }

        AlignAppGroupToTop(group);
        HideLetterIndex(focusGroup: group);
    }

    private void AlignAppGroupToTop(CollectionViewGroup group)
    {
        AppsScrollViewer.UpdateLayout();
        if (AppsList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement container)
        {
            var groupTop = container.TranslatePoint(new System.Windows.Point(), AppsScrollViewer).Y;
            AppsScrollViewer.ScrollToVerticalOffset(Math.Max(0, AppsScrollViewer.VerticalOffset + groupTop));
            AppsScrollViewer.UpdateLayout();
        }
    }

    private void HideLetterIndex(bool animate = true, CollectionViewGroup? focusGroup = null)
    {
        _isLetterIndexActive = false;
        AppsScrollViewer.IsHitTestVisible = false;
        LetterIndexPanel.IsHitTestVisible = false;

        if (LetterIndexPanel.Visibility != Visibility.Visible)
        {
            ResetSemanticZoomVisuals();
            AppsScrollViewer.IsHitTestVisible = true;
            FocusAppGroup(focusGroup);
            return;
        }

        BeginSemanticZoomTransition(
            zoomedInViewActive: true,
            animate,
            () =>
            {
                LetterIndexPanel.Visibility = Visibility.Collapsed;
                AppsScrollViewer.IsHitTestVisible = true;
                FocusAppGroup(focusGroup);
            });
    }

    private void FocusAppGroup(CollectionViewGroup? group)
    {
        if (group is null)
        {
            return;
        }

        AppsScrollViewer.UpdateLayout();
        if (AppsList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement container)
        {
            container.Focus();
        }
    }

    private void SemanticZoomViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isSemanticZoomAnimating)
        {
            ResetSemanticZoomVisuals();
        }
    }

    private void BeginSemanticZoomTransition(bool zoomedInViewActive, bool animate, Action? completed = null)
    {
        var viewport = new System.Windows.Size(SemanticZoomViewport.ActualWidth, SemanticZoomViewport.ActualHeight);
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            ResetSemanticZoomVisuals();
            completed?.Invoke();
            return;
        }

        var generation = ++_semanticZoomAnimationGeneration;
        var animationsEnabled = animate && SystemParameters.ClientAreaAnimation;
        _isSemanticZoomAnimating = animationsEnabled;
        SemanticZoomMotion.Animate(
            viewport,
            zoomedInViewActive,
            SemanticZoomSharedScale,
            SemanticZoomSharedTranslate,
            SemanticZoomedInScale,
            SemanticZoomedInTranslate,
            ZoomedInPresenter,
            LetterIndexPanel,
            animationsEnabled,
            () =>
            {
                if (generation != _semanticZoomAnimationGeneration)
                {
                    return;
                }

                _isSemanticZoomAnimating = false;
                ResetSemanticZoomVisuals();
                completed?.Invoke();
            });
    }

    private void ResetSemanticZoomVisuals()
    {
        var viewport = new System.Windows.Size(SemanticZoomViewport.ActualWidth, SemanticZoomViewport.ActualHeight);
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        SemanticZoomMotion.Snap(
            viewport,
            !_isLetterIndexActive,
            SemanticZoomSharedScale,
            SemanticZoomSharedTranslate,
            SemanticZoomedInScale,
            SemanticZoomedInTranslate,
            ZoomedInPresenter,
            LetterIndexPanel);
    }

    private void ClearSearch()
    {
        SearchBox.Clear();
        SearchPanel.Visibility = Visibility.Collapsed;
        HideLetterIndex(animate: false);
        RecentPanel.Visibility = Visibility.Visible;
        AppsView.Filter = null;
        AppsView.Refresh();
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
            AppLauncher.OpenFileLocation(app);
        }
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

    private static bool MatchesApp(AppEntry app, string query) =>
        query.Length == 0
        || app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || app.Children.Any(child => MatchesApp(child, query));

    private static bool ExpandMatchingFolders(IEnumerable<AppEntry> entries, string query)
    {
        var anyMatch = false;
        foreach (var entry in entries)
        {
            var childMatch = entry.IsFolder && ExpandMatchingFolders(entry.Children, query);
            if (entry.IsFolder)
            {
                entry.IsExpanded = childMatch;
            }

            anyMatch |= childMatch || entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }

        return anyMatch;
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

        var dialog = new TileSettingsWindow(tile);
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
                    new System.Windows.Size(item.container!.ActualWidth, item.container.ActualHeight))))
            .ToArray();
        _groupDragTargets = TileGroupDropResolver.IncludeEmptyColumns(existingTargets, groupColumns);
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
            .Select(GetGroupContainer)
            .Where(container => container is not null)
            .Select(container => (int)Math.Round(
                GetGroupLayoutPosition(container!).X / Win10TileMetrics.GroupPitch) + 1)
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
        var (column, row) = TileDropResolver.GetCell(position, _dragAnchor, tile);
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
        var (column, row) = TileDropResolver.GetCell(logicalPosition, _dragAnchor, tile);
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

    private bool AddAppTile(TileGroup target, AppEntry app, System.Windows.Point position)
    {
        var tile = CreateAppTile(app);
        var (column, row) = TileDropResolver.GetCell(position, _appDragAnchor, tile);
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

            (int Column, int Row) location = added
                ? Win10GroupLayout.FindFirstAvailable(target, tile)
                : (
                    Math.Clamp((int)Math.Round(position.X / Win10TileMetrics.CellPitch), 0,
                        Win10TileMetrics.GroupColumns - tile.Size.ColumnSpan()),
                    Math.Max(0, (int)Math.Round(position.Y / Win10TileMetrics.CellPitch)));
            added |= Win10GroupLayout.Add(target, tile, location.Column, location.Row);
        }

        if (added)
        {
            TileLayoutStore.Save(TileLayout);
        }

        return added;
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
            tile.Icon = ShellIconLoader.Load(tile.IconPath);
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

            tile.Icon = app.Icon;
            return;
        }

        tile.Icon = ShellIconLoader.Load(tile.LaunchTarget);
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

    private void TopResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginResize(HtTop, e);
    }

    private void RightResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginResize(HtRight, e);
    }

    private void TopRightResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginResize(HtTopRight, e);
    }

    private void BeginResize(int hitTest, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ReleaseMouseCapture();
        SendMessage(new WindowInteropHelper(this).Handle, WmNcLButtonDown, hitTest, 0);
        if (hitTest is HtRight or HtTopRight)
        {
            SnapWindowWidthAfterResize();
        }
        else
        {
            SaveCurrentSize();
        }
    }

    private void SnapWindowWidthAfterResize()
    {
        var currentWidth = ActualWidth > 0 ? ActualWidth : Width;
        var targetWidth = StartWindowSizing.SnapWidth(currentWidth, MaxWidth);
        if (Math.Abs(currentWidth - targetWidth) < 0.5 || !SystemParameters.ClientAreaAnimation)
        {
            Width = targetWidth;
            PositionOnCurrentMonitor();
            SaveCurrentSize();
            return;
        }

        StopWindowWidthSnapAnimation();
        _isWindowWidthSnapAnimating = true;
        _windowWidthSnapStartedAt = Environment.TickCount64;
        _windowWidthSnapFrom = currentWidth;
        _windowWidthSnapTo = targetWidth;
        _windowWidthSnapRight = Left + currentWidth;
        _windowWidthSnapGroupPositions = CaptureGroupReorderPositions();
        CompositionTarget.Rendering += WindowWidthSnap_Rendering;
    }

    private void WindowWidthSnap_Rendering(object? sender, EventArgs e)
    {
        if (!_isWindowWidthSnapAnimating)
        {
            return;
        }

        var elapsed = Environment.TickCount64 - _windowWidthSnapStartedAt;
        var progress = elapsed / (double)StartWindowResizeMotion.DurationMilliseconds;
        var width = StartWindowResizeMotion.Interpolate(_windowWidthSnapFrom, _windowWidthSnapTo, progress);
        Width = width;
        if (_taskbarEdge == TaskbarEdge.Right)
        {
            Left = _windowWidthSnapRight - width;
        }

        if (progress < 1)
        {
            return;
        }

        var previousGroupPositions = _windowWidthSnapGroupPositions;
        StopWindowWidthSnapAnimation();
        Width = _windowWidthSnapTo;
        PositionOnCurrentMonitor();
        UpdateLayout();
        if (previousGroupPositions is not null)
        {
            AnimateGroupReorderFrom(previousGroupPositions);
        }

        SaveCurrentSize();
    }

    private void StopWindowWidthSnapAnimation()
    {
        if (_isWindowWidthSnapAnimating)
        {
            CompositionTarget.Rendering -= WindowWidthSnap_Rendering;
        }

        _isWindowWidthSnapAnimating = false;
        _windowWidthSnapGroupPositions = null;
    }

    private void NavigationToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _navigationPinnedOpen = !_navigationPinnedOpen;
        SetNavigationExpanded(_navigationPinnedOpen);
    }

    private void NavigationToggleButton_MouseEnter(object sender, MouseEventArgs e) =>
        SetNavigationExpanded(true);

    private void NavigationPane_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_navigationPinnedOpen && _openContextMenuCount == 0)
        {
            SetNavigationExpanded(false);
        }
    }

    private void SetNavigationExpanded(bool expanded)
    {
        if (_navigationExpanded == expanded)
        {
            return;
        }

        _navigationExpanded = expanded;
        var targetWidth = expanded
            ? Win10VisualMetrics.ExpandedNavigationWidth
            : Win10VisualMetrics.CollapsedNavigationWidth;
        NavigationToggleButton.ToolTip = expanded ? "收起" : "展开";
        if (expanded)
        {
            NavigationPane.Background = (System.Windows.Media.Brush)FindResource("ExpandedNavigationBackground");
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            NavigationPane.Width = targetWidth;
            if (!expanded)
            {
                NavigationPane.Background = System.Windows.Media.Brushes.Transparent;
            }

            return;
        }

        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = NavigationPane.ActualWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
            },
        };
        animation.Completed += (_, _) =>
        {
            NavigationPane.BeginAnimation(WidthProperty, null);
            NavigationPane.Width = targetWidth;
            if (!expanded && !_navigationExpanded)
            {
                NavigationPane.Background = System.Windows.Media.Brushes.Transparent;
            }
        };
        NavigationPane.BeginAnimation(WidthProperty, animation);
    }

    private void NavigationPreferencesMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is string key)
            {
                item.IsChecked = _navigationPreferences.IsVisible(key);
            }
        }
    }

    private void NavigationPreference_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string key } item)
        {
            return;
        }

        _navigationPreferences.SetVisible(key, item.IsChecked);
        NavigationPreferencesStore.Save(_navigationPreferences);
        ApplyNavigationPreferences();
    }

    private void ApplyNavigationPreferences()
    {
        UserNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowUser));
        DocumentsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowDocuments));
        DownloadsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowDownloads));
        PicturesNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowPictures));
        MusicNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowMusic));
        VideosNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowVideos));
        FileExplorerNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowFileExplorer));
        NetworkNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowNetwork));
        SettingsNavigationButton.Visibility = PreferenceVisibility(nameof(NavigationPreferences.ShowSettings));
    }

    private Visibility PreferenceVisibility(string key) =>
        _navigationPreferences.IsVisible(key) ? Visibility.Visible : Visibility.Collapsed;

    private void UserNavigationButton_Click(object sender, RoutedEventArgs e) =>
        OpenButtonContextMenu(UserNavigationButton);

    private void PowerNavigationButton_Click(object sender, RoutedEventArgs e) =>
        OpenButtonContextMenu(PowerNavigationButton);

    private static void OpenButtonContextMenu(Button button)
    {
        if (button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
        button.ContextMenu.IsOpen = true;
    }

    private void DocumentsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

    private void DownloadsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("下载", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

    private void PicturesNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("图片", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

    private void MusicNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("音乐", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

    private void VideosNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("视频", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

    private void FileExplorerNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("文件资源管理器", "explorer.exe");

    private void NetworkNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("网络", "shell:NetworkPlacesFolder");

    private void SettingsNavigationButton_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("设置", "ms-settings:");

    private void AccountSettings_Click(object sender, RoutedEventArgs e) =>
        LaunchNavigationTarget("账户设置", "ms-settings:yourinfo");

    private void LockSession_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        LockWorkStation();
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchProcess("注销", "shutdown.exe", "/l");
    }

    private void Sleep_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        SetSuspendState(false, false, false);
    }

    private void ShutDown_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchProcess("关机", "shutdown.exe", "/s /t 0");
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchProcess("重启", "shutdown.exe", "/r /t 0");
    }

    private void LaunchNavigationTarget(string name, string target)
    {
        DismissWindow(yieldTopmost: true);
        AppLauncher.LaunchShellTarget(name, target);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    private delegate bool EnumWindowsProcedure(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProcedure callback, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height,
        uint flags);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool AnimateWindow(nint window, int durationMilliseconds, uint flags);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(nint window, ref WindowCompositionAttributeData data);
}