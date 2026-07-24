using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using TileStart.Host.Applications;
using TileStart.Host.Shell;
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

    public MainWindow()
    {
        InitializeComponent();
        _appController = new Controllers.ApplicationPaneController(
            TileLayout,
            Dispatcher,
            NavigationToggleButton,
            WindowRoot,
            showFromShell: ShowFromShell,
            dismissWindow: DismissWindow,
            toggleAppFolderAsync: folder => _tileWorkspaceController!.ToggleAppFolderAsync(folder),
            pinTileToStart: tile => _tileWorkspaceController!.PinTileToStart(tile),
            ensureGroupGridCoordinates: () => _tileDragCoordinator!.EnsureGroupGridCoordinates(),
            prepareMotionElements: PrepareMotionElements,
            updateLayout: () => UpdateLayout());
        _tileDragCoordinator = new Controllers.TileDragCoordinator(
            this,
            MainSurface,
            TilePane,
            TileScrollViewer,
            TileGroupsControl,
            InternalDragPreview,
            InternalDragPreviewTransform,
            TileLayout,
            _appController,
            findTileLocation: (TileItem tile, out TileGroup group, out TileItem? folder) =>
                _tileWorkspaceController!.FindTileLocation(tile, out group, out folder),
            setSuppressTileActivationUntil: value => _suppressTileActivationUntil = value,
            captureElement: CaptureElement);
        _navigationController = new Controllers.NavigationController(
            NavigationPane,
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
            getOpenContextMenuCount: () => _openContextMenuCount,
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
            getOpenContextMenuCount: () => _openContextMenuCount,
            setOpenContextMenuCount: value => _openContextMenuCount = value,
            getSuppressTileActivationUntil: () => _suppressTileActivationUntil);
        _controller = new StartWindowController(
            this,
            WindowRoot,
            MainSurface,
            beforeShow: () =>
            {
                if (_appController.CheckAndRemoveMissingApps())
                    _appController.RefreshApplicationCollection();
            },
            clearSearch: _navigationController.ClearSearch,
            ensureTileScrollBarClearance: () => _tileDragCoordinator.EnsureTileScrollBarClearance(),
            captureGroupReorderPositions: () => _tileDragCoordinator.CaptureGroupReorderPositions(),
            animateGroupReorderFrom: p => _tileDragCoordinator.AnimateGroupReorderFrom(p),
            isAnyDragActive: () => _tileDragCoordinator.IsDragging,
            hasOpenContextMenu: () => _openContextMenuCount > 0);
        _navigationController.ApplyNavigationPreferences();
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

    public void ShowFromShell() => _controller.ShowFromShell();

    public void AllowClose() => _controller.AllowClose();

    protected override void OnClosing(CancelEventArgs e)
    {
        _controller.OnClosing(e);
        _navigationController.StopHoverTimer();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _tileDragCoordinator?.StopTileDragAutoScroll();
        _navigationController.StopHoverTimer();
        _controller.OnClosed();
        base.OnClosed(e);
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