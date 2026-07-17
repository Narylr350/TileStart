using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using ItemsControl = System.Windows.Controls.ItemsControl;
using MenuItem = System.Windows.Controls.MenuItem;

namespace TileStart.Host;

public partial class MainWindow : Window
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint SwpShowWindow = 0x0040;
    private const uint AwHide = 0x00010000;
    private const uint AwBlend = 0x00080000;
    private const int DismissDurationMilliseconds = 150;
    private const int CollapsedRecentAppCount = 3;
    private const int ExpandedRecentAppCount = 6;
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopRight = 14;
    private static readonly nint HwndTopmost = new(-1);
    private readonly RangeObservableCollection<AppEntry> _apps = [];
    private AppEntry[] _launchableApps = [];
    private AppEntry[] _recentAppCandidates = [];
    private bool _allowClose;
    private bool _recentAppsExpanded;
    private bool _isDismissing;
    private TaskbarEdge _taskbarEdge = TaskbarEdge.Bottom;
    private System.Windows.Point _dragStart;
    private TileItem? _dragTile;
    private TileGroup? _dragSource;
    private TileDragTransaction? _dragTransaction;
    private bool _dragCompleted;

    public MainWindow()
    {
        AppsView = new ListCollectionView(_apps);
        AppsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppEntry.SortLetter)));
        AppsView.SortDescriptions.Add(new SortDescription(nameof(AppEntry.SortLetter), ListSortDirection.Ascending));
        AppsView.SortDescriptions.Add(new SortDescription(nameof(AppEntry.Name), ListSortDirection.Ascending));
        InitializeComponent();
        DataContext = this;
        var savedSize = WindowSizeStore.Load();
        if (savedSize is not null)
        {
            Width = Math.Max(MinWidth, savedSize.Value.Width);
            Height = Math.Max(MinHeight, savedSize.Value.Height);
        }

        _ = LoadAppsAsync();
    }

    public ObservableCollection<AppEntry> RecentApps { get; } = [];

    public ICollectionView AppsView { get; }

    public IReadOnlyList<AlphabetIndexEntry> AlphabetLetters { get; } = AlphabetIndex.Create();

    public TileLayout TileLayout { get; } = new();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableAcrylic();
    }

    public void ShowFromShell()
    {
        if (IsVisible)
        {
            DismissWindow();
            return;
        }

        PositionOnCurrentMonitor();
        PrepareMotionElements();
        var motionElements = FindMotionElements(MainSurface).ToArray();
        var animationsEnabled = SystemParameters.ClientAreaAnimation;
        StartMotion.StageEntrance(MainSurface, motionElements, _taskbarEdge == TaskbarEdge.Bottom, animationsEnabled);
        Show();
        UpdateLayout();
        PositionOnCurrentMonitor();
        StartMotion.PlayEntrance(motionElements, animationsEnabled);
        Activate();
        Focus();
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
            Hide();
        }

        base.OnClosing(e);
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

            if (savedLayout is null)
            {
                TileLayoutStore.Save(TileLayout);
            }

            PrepareMotionElements();
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application list load failed: {exception}");
        }
    }

    private void PrepareMotionElements()
    {
        if (IsVisible)
        {
            return;
        }

        var size = new System.Windows.Size(Math.Max(MinWidth, Width), Math.Max(MinHeight, Height));
        MainSurface.Measure(size);
        MainSurface.Arrange(new System.Windows.Rect(new System.Windows.Point(), size));
        MainSurface.UpdateLayout();
        StartMotion.Prepare(FindMotionElements(MainSurface));
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
        var taskbarRect = FindTaskbarRect(monitor);
        var edge = StartWindowPlacement.InferTaskbarEdge(monitorRect, taskbarRect);
        _taskbarEdge = edge;
        var logicalWidth = ActualWidth > 0 ? ActualWidth : Width;
        var logicalHeight = ActualHeight > 0 ? ActualHeight : Height;
        var placement = StartWindowPlacement.Calculate(
            ToPixelRect(monitorInfo.WorkArea),
            edge,
            (int)Math.Round(logicalWidth * dpi / 96.0),
            (int)Math.Round(logicalHeight * dpi / 96.0));
        var scale = 96.0 / dpi;
        Left = placement.Left * scale;
        Top = placement.Top * scale;
        Width = placement.Width * scale;
        Height = placement.Height * scale;

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        var positioned = SetWindowPos(handle, HwndTopmost, placement.Left, placement.Top, placement.Width, placement.Height, SwpShowWindow);
        DiagnosticLog.Write($"Window placement: monitor={monitorRect}, work={ToPixelRect(monitorInfo.WorkArea)}, taskbar={taskbarRect}, edge={edge}, target={placement}, positioned={positioned}, error={(positioned ? 0 : Marshal.GetLastWin32Error())}.");
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

    private void EnableAcrylic()
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var accent = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = 2,
            GradientColor = unchecked((int)0xCC202020),
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
            SetWindowCompositionAttribute(new WindowInteropHelper(this).Handle, ref data);
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

    private async void Window_Deactivated(object? sender, EventArgs e)
    {
        SaveCurrentSize();
        ClearSearch();
        while (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            await Task.Delay(50);
        }

        var hasActiveOwnedWindow = OwnedWindows.Cast<Window>().Any(window => window.IsActive);
        if (StartWindowLifecycle.ShouldHideAfterDeactivation(IsActive, hasActiveOwnedWindow))
        {
            DismissWindow(yieldTopmost: true);
        }
    }

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
            if (LetterIndexPanel.Visibility == Visibility.Visible)
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

        HideLetterIndex();
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
        HideLetterIndex();
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

        AppsScrollViewer.Visibility = Visibility.Collapsed;
        LetterIndexPanel.Visibility = Visibility.Visible;
    }

    private void AlphabetLetter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AlphabetIndexEntry { IsAvailable: true } entry })
        {
            return;
        }

        HideLetterIndex();
        if (entry.IsRecent)
        {
            AppsScrollViewer.ScrollToTop();
            RecentPanel.BringIntoView();
            return;
        }

        var group = AppsView.Groups?
            .OfType<CollectionViewGroup>()
            .FirstOrDefault(candidate => candidate.Name?.ToString()?.Equals(entry.TargetLetter, StringComparison.OrdinalIgnoreCase) == true);
        if (group is null)
        {
            return;
        }

        AppsScrollViewer.UpdateLayout();
        if (AppsList.ItemContainerGenerator.ContainerFromItem(group) is FrameworkElement container)
        {
            var groupTop = container.TranslatePoint(new System.Windows.Point(), AppsScrollViewer).Y;
            AppsScrollViewer.ScrollToVerticalOffset(Math.Max(0, AppsScrollViewer.VerticalOffset + groupTop));
            container.Focus();
        }
    }

    private void HideLetterIndex()
    {
        LetterIndexPanel.Visibility = Visibility.Collapsed;
        AppsScrollViewer.Visibility = Visibility.Visible;
    }

    private void ClearSearch()
    {
        SearchBox.Clear();
        SearchPanel.Visibility = Visibility.Collapsed;
        HideLetterIndex();
        RecentPanel.Visibility = Visibility.Visible;
        AppsView.Filter = null;
        AppsView.Refresh();
    }

    private void AppButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AppEntry app })
        {
            return;
        }

        if (app.IsFolder)
        {
            app.IsExpanded = !app.IsExpanded;
            return;
        }

        if (AppLauncher.Launch(app))
        {
            DismissWindow(yieldTopmost: true);
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
        if (_dragCompleted)
        {
            _dragCompleted = false;
            return;
        }

        if (sender is Button { Tag: TileItem tile } && AppLauncher.Launch(tile))
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

        var dialog = new TileSettingsWindow(tile) { Owner = this };
        if (dialog.ShowDialog() != true)
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
        var dialog = new TileSettingsWindow(tile, true) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ApplyTileSettings(tile, dialog);
        var group = TileLayout.Groups.LastOrDefault();
        if (group is null)
        {
            group = new TileGroup();
            TileLayout.Groups.Add(group);
        }

        var location = Win10GroupLayout.FindFirstAvailable(group, tile);
        Win10GroupLayout.Add(group, tile, location.Column, location.Row);
        TileLayoutStore.Save(TileLayout);
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        TileGroupManager.Add(TileLayout);
        TileLayoutStore.Save(TileLayout);
    }

    private void GroupName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
        {
            return;
        }

        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void GroupName_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        TileLayoutStore.Save(TileLayout);
    }

    private void MoveGroupLeft_Click(object sender, RoutedEventArgs e)
    {
        MoveGroup(sender, -1);
    }

    private void MoveGroupRight_Click(object sender, RoutedEventArgs e)
    {
        MoveGroup(sender, 1);
    }

    private void MoveGroup(object sender, int offset)
    {
        var group = GetContextGroup(sender);
        if (group is not null && TileGroupManager.Move(TileLayout, group, offset))
        {
            TileLayoutStore.Save(TileLayout);
        }
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

    private void TileButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragCompleted = false;
        _dragStart = e.GetPosition(this);
        _dragTile = (sender as Button)?.Tag as TileItem;
        _dragSource = _dragTile is null ? null : TileLayout.Groups.FirstOrDefault(group => group.Tiles.Contains(_dragTile));
    }

    private void TileButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _dragTile is null
            || _dragSource is null
            || _dragTransaction is not null)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var tile = _dragTile;
        _dragCompleted = true;
        var transaction = new TileDragTransaction(TileLayout, _dragSource, tile);
        _dragTransaction = transaction;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(TileItem), tile), DragDropEffects.Move);
        }
        finally
        {
            transaction.Dispose();
            if (ReferenceEquals(_dragTransaction, transaction))
            {
                _dragTransaction = null;
            }

            _dragTile = null;
            _dragSource = null;
        }
    }

    private void TileGroup_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is ItemsControl { Tag: TileGroup target } itemsControl
            && e.Data.GetData(typeof(TileItem)) is TileItem tile
            && _dragTransaction is not null)
        {
            var (column, row) = GetDropCell(e.GetPosition(itemsControl), tile);
            e.Effects = _dragTransaction.Preview(target, column, row)
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
            var (column, row) = GetDropCell(position, tile);
            if (_dragTransaction.Preview(target, column, row))
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

    private void TileArea_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TileItem)) && _dragTransaction is not null)
        {
            _dragTransaction.PreviewNewGroup();
            e.Effects = DragDropEffects.Move;
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
        if (e.Data.GetDataPresent(typeof(TileItem)) && _dragTransaction?.PreviewTarget is not null)
        {
            _dragTransaction.Commit();
            TileLayoutStore.Save(TileLayout);
            e.Effects = DragDropEffects.Move;
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

    private static (int Column, int Row) GetDropCell(System.Windows.Point position, TileItem tile)
    {
        var column = Math.Clamp((int)Math.Round(position.X / Win10TileMetrics.CellPitch),
                                0,
                                Win10TileMetrics.GroupColumns - tile.Size.ColumnSpan());
        var row = Math.Max(0, (int)Math.Round(position.Y / Win10TileMetrics.CellPitch));
        return (column, row);
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
                : (Math.Clamp((int)Math.Round(position.X / Win10TileMetrics.CellPitch), 0, Win10TileMetrics.GroupColumns - tile.Size.ColumnSpan()),
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
            tile.BackgroundImage = ShellIconLoader.LoadImage(tile.BackgroundImagePath);
            RestoreTileIcon(tile, apps);
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

        var app = apps.FirstOrDefault(candidate => candidate.LaunchTarget.Equals(tile.LaunchTarget, StringComparison.OrdinalIgnoreCase));
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

    private static IEnumerable<FrameworkElement> FindMotionElements(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element && StartMotion.GetIsEntranceTarget(element))
            {
                yield return element;
            }

            foreach (var descendant in FindMotionElements(child))
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
        SaveCurrentSize();
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
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool AnimateWindow(nint window, int durationMilliseconds, uint flags);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(nint window, ref WindowCompositionAttributeData data);
}
