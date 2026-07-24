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

public partial class MainWindow : Window
{

    private readonly System.Windows.Threading.DispatcherTimer _foregroundWatchdogTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(50),
    };

    private readonly System.Windows.Threading.DispatcherTimer _navigationHoverTimer = new()
    {
        Interval = SystemParameters.MouseHoverTime,
    };

    private bool _navigationExpanded;
    private bool _navigationPinnedOpen;
    private readonly NavigationPreferences _navigationPreferences = NavigationPreferencesStore.Load();
    private int _semanticZoomAnimationGeneration;
    private int _foregroundActivationGeneration;
    private bool _isLetterIndexActive;
    private bool _isSemanticZoomAnimating;
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
        _navigationHoverTimer.Tick += NavigationHoverTimer_Tick;
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

        if (_applicationContentReady && RemoveMissingApplications(_apps))
        {
            RefreshApplicationCollection();
        }

        CancelEntranceSnapshot();
        ApplyWindowMaterial();
        _foregroundLifecycle.Reset();
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
        var activationGeneration = ++_foregroundActivationGeneration;
        TryAcquireForeground(activationGeneration, 0);

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
            _navigationHoverTimer.Stop();
            Hide();
        }

        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        StopWindowWidthSnapAnimation();
        StopTileDragAutoScroll();
        _foregroundWatchdogTimer.Stop();
        _navigationHoverTimer.Stop();
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
}
