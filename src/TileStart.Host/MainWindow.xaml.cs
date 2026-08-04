using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using TileStart.Host.Applications;
using TileStart.Host.Persistence;
using TileStart.Host.Shell;
using TileStart.Host.Themes;
using TileStart.Host.Windowing;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host;

public partial class MainWindow : Window
{
    private readonly StartWindowController _controller;
    private readonly Controllers.ApplicationPaneController _appController;
    private readonly Controllers.TileDragCoordinator _tileDragCoordinator;
    private readonly Controllers.NavigationController _navigationController;
    private readonly Controllers.TileWorkspaceController _tileWorkspaceController;
    private readonly HashSet<TileGroup> _observedTileGroups = [];
    private bool _controllersDisposed;

    public MainWindow(AppThemeStyle themeStyle = AppThemeStyle.Windows11, bool useDarkMode = true)
    {
        InitializeComponent();
        MinWidth = StartWindowSizing.WidthForColumns(StartWindowSizing.MinimumGroupColumns);
        MaxWidth = StartWindowSizing.WidthForColumns(StartWindowSizing.MaximumGroupColumns);
        var savedSize = WindowSizeStore.Load();
        var savedLayout = TileLayoutStore.Load();
        var savedTileCount = savedLayout?.Groups.Sum(group => group.Tiles.Count) ?? 0;
        var widestSavedGroup = savedLayout?.Groups
            .Where(group => group.Tiles.Count > 0)
            .Select(group => group.WidthUnits)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        var preferredWorkspaceColumns = StartWindowSizing.InitialWorkspaceColumns(
            savedSize?.WorkspaceColumns,
            savedTileCount,
            widestSavedGroup);
        var preferredHeight = savedSize?.Height ?? Height;
        Width = StartWindowSizing.WidthForColumns(preferredWorkspaceColumns);
        Height = Math.Max(MinHeight, preferredHeight);
        _appController = new Controllers.ApplicationPaneController(
            TileLayout,
            savedLayout,
            Dispatcher,
            NavigationToggleButton,
            WindowRoot,
            showFromShell: () => _controller!.ShowFromShell(),
            isWindowVisible: () => _controller?.IsWindowVisible ?? false,
            dismissWindow: DismissWindow,
            toggleAppFolderAsync: folder => _tileWorkspaceController!.ToggleAppFolderAsync(folder),
            pinTileToStart: tile => _tileWorkspaceController!.PinTileToStart(tile),
            ensureGroupGridCoordinates: () => _tileDragCoordinator!.EnsureGroupGridCoordinates(),
            updateLayout: () => UpdateLayout());
        _tileDragCoordinator = new Controllers.TileDragCoordinator(
            this,
            MainSurface,
            TilePane,
            TileScrollViewer,
            TileGroupsControl,
            InternalDragPreview,
            InternalDragPreviewScaleTransform,
            InternalDragPreviewTransform,
            TileLayout,
            _appController,
            findTileLocation: (TileItem tile, out TileGroup group, out TileItem? folder) =>
                _tileWorkspaceController!.FindTileLocation(tile, out group, out folder),
            setSuppressTileActivationUntil: value => _suppressTileActivationUntil = value,
            captureElement: CaptureElement);
        _navigationController = new Controllers.NavigationController(
            NavigationPane,
            NavigationBackdrop,
            NavigationToggleButton,
            UserNavigationButton,
            DocumentsNavigationButton,
            DownloadsNavigationButton,
            PicturesNavigationButton,
            MusicNavigationButton,
            VideosNavigationButton,
            FileExplorerNavigationButton,
            NetworkNavigationButton,
            SettingsNavigationButton,
            PowerNavigationButton,
            PowerUpdateBadge,
            UpdateAndShutDownMenuItem,
            UpdateAndRestartMenuItem,
            LetterIndexPanel,
            SearchPanel,
            SearchBox,
            AppsView,
            AppsList,
            _appController.RecentSection,
            SemanticZoomViewport,
            SemanticZoomSharedScale,
            SemanticZoomSharedTranslate,
            SemanticZoomedInScale,
            SemanticZoomedInTranslate,
            ZoomedInPresenter,
            dismissWindow: DismissWindow,
            cancelCurrentDrag: () => _tileDragCoordinator?.CancelCurrentDrag() ?? false,
            getAllApps: () => _appController.AllApps,
            hasOpenContextMenu: () => _hasOpenContextMenu,
            lockWorkStation: () => LockWorkStation(),
            setSuspendState: (h, f, d) => SetSuspendState(h, f, d));
        _tileWorkspaceController = new Controllers.TileWorkspaceController(
            this,
            TileLayout,
            _tileDragCoordinator,
            _appController,
            _navigationController,
            NavigationPane,
            TileGroupsControl,
            AppsList,
            dismissWindow: DismissWindow,
            tryDismissAfterForegroundChange: TryDismissAfterForegroundChange,
            setOpenContextMenuState: value => _hasOpenContextMenu = value,
            getSuppressTileActivationUntil: () => _suppressTileActivationUntil);
        _controller = new StartWindowController(
            this,
            WindowRoot,
            MainSurface,
            beforeShow: () =>
            {
                _hasOpenContextMenu = false;
                _ = _navigationController.RefreshWindowsUpdateStateAsync();
            },
            clearSearch: _navigationController.ClearSearch,
            ensureTileScrollBarClearance: () => _tileDragCoordinator.EnsureTileScrollBarClearance(),
            captureGroupReorderPositions: () => _tileDragCoordinator.CaptureGroupReorderPositions(),
            animateGroupReorderFrom: p => _tileDragCoordinator.AnimateGroupReorderFrom(p),
            isAnyDragActive: () => _tileDragCoordinator.IsDragging,
            hasOpenContextMenu: () => _hasOpenContextMenu,
            cancelActiveDrag: () => { _tileDragCoordinator.CancelCurrentDrag(); },
            preferredWorkspaceColumns,
            preferredHeight,
            themeStyle,
            useDarkMode);
        _controller.WindowDismissing += _tileWorkspaceController.CloseOpenContextMenu;
        _navigationController.ApplyNavigationPreferences();
        DataContext = this;
        TileLayout.Groups.CollectionChanged += TileGroups_CollectionChanged;
        _appController.RestoreSavedLayout();
        _controller.ScheduleInitialMotionPreparation();
        _ = _appController.LoadAppsAsync();
    }

    public ObservableCollection<AppEntry> RecentApps => _appController.RecentApps;

    public string CurrentUserName => _appController.CurrentUserName;

    public ImageSource? CurrentUserPicture => _appController.CurrentUserPicture;

    public ICollectionView AppsView => _appController.AppsView;

    public IReadOnlyList<AlphabetIndexEntry> AlphabetLetters => _appController.AlphabetLetters;

    public TileLayout TileLayout { get; } = new();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _controller.SetWindowSource(PresentationSource.FromVisual(this) as HwndSource);
        _controller.ApplyWindowMaterial();
    }

    public void ShowFromShell() => _appController.ShowFromShellWhenReady();

    public void AllowClose() => _controller.AllowClose();

    public void PrepareForApplicationRestart()
    {
        _tileWorkspaceController.CloseOpenContextMenu();
        _controller.AllowClose();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _controller.OnClosing(e);
        _navigationController.StopHoverTimer();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        DisposeControllers();
        base.OnClosed(e);
    }

    private void DisposeControllers()
    {
        if (_controllersDisposed)
        {
            return;
        }

        _controllersDisposed = true;
        TileLayout.Groups.CollectionChanged -= TileGroups_CollectionChanged;
        foreach (var group in _observedTileGroups)
        {
            group.Tiles.CollectionChanged -= GroupTiles_CollectionChanged;
            group.PropertyChanged -= TileGroup_PropertyChanged;
        }

        _observedTileGroups.Clear();
        _controller.Dispose();
        _tileWorkspaceController.Dispose();
        _tileDragCoordinator.Dispose();
        _navigationController.Dispose();
        _appController.Dispose();
    }

    private void TileGroups_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var group in e.OldItems.OfType<TileGroup>())
            {
                group.Tiles.CollectionChanged -= GroupTiles_CollectionChanged;
                group.PropertyChanged -= TileGroup_PropertyChanged;
                _observedTileGroups.Remove(group);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var group in e.NewItems.OfType<TileGroup>())
            {
                if (_observedTileGroups.Add(group))
                {
                    group.Tiles.CollectionChanged += GroupTiles_CollectionChanged;
                    group.PropertyChanged += TileGroup_PropertyChanged;
                }
            }
        }

        UpdateMinimumWidthForTileLayout();
    }

    private void GroupTiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateMinimumWidthForTileLayout();

    private void TileGroup_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TileGroup.WidthUnits))
        {
            UpdateMinimumWidthForTileLayout();
        }
    }

    private void UpdateMinimumWidthForTileLayout()
    {
        var tileCount = TileLayout.Groups.Sum(group => group.Tiles.Count);
        var widestGroup = TileLayout.Groups
            .Where(group => group.Tiles.Count > 0)
            .Select(group => group.WidthUnits)
            .DefaultIfEmpty(0)
            .Max();
        _controller.SetMinimumWorkspaceColumns(
            StartWindowSizing.MinimumColumnsForTileLayout(tileCount, widestGroup));
    }

    private void Window_Deactivated(object? sender, EventArgs e) =>
        _controller.WindowDeactivated();

    private void TopResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _controller.TopResizeBorder_MouseLeftButtonDown(sender, e);

    private void RightResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _controller.RightResizeBorder_MouseLeftButtonDown(sender, e);

    private void TopRightResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _controller.TopRightResizeBorder_MouseLeftButtonDown(sender, e);

    private void DismissWindow(bool yieldTopmost = false) => _controller.DismissWindow(yieldTopmost);

    private void PrepareMotionElements() => _controller.PrepareMotionElements();

    private void TryDismissAfterForegroundChange(string trigger) =>
        _controller.TryDismissAfterForegroundChange(trigger);

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _tileDragCoordinator?.CancelCurrentDrag() == true)
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}