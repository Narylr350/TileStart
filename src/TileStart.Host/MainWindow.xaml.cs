using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Button = System.Windows.Controls.Button;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using ItemsControl = System.Windows.Controls.ItemsControl;

namespace TileStart.Host;

public partial class MainWindow : Window
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint SwpShowWindow = 0x0040;
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopRight = 14;
    private static readonly nint HwndTopmost = new(-1);
    private readonly ObservableCollection<AppEntry> _apps = [];
    private bool _allowClose;
    private System.Windows.Point _dragStart;
    private TileItem? _dragTile;
    private TileGroup? _dragSource;
    private bool _dragCompleted;

    public MainWindow()
    {
        AppsView = new ListCollectionView(_apps);
        AppsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppEntry.SortLetter)));
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
            SaveCurrentSize();
            ClearSearch();
            Hide();
            return;
        }

        Show();
        UpdateLayout();
        PositionOnCurrentMonitor();
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
            foreach (var app in apps)
            {
                _apps.Add(app);
            }

            foreach (var app in apps.Where(app => app.AddedAt > DateTime.MinValue).OrderByDescending(app => app.AddedAt).Take(3))
            {
                RecentApps.Add(app);
            }

            var savedLayout = TileLayoutStore.Load();
            var layout = savedLayout ?? DefaultTileLayout.Create(apps);
            RestoreTileIcons(layout, apps);
            foreach (var group in layout.Groups)
            {
                TileLayout.Groups.Add(group);
            }

            if (savedLayout is null)
            {
                TileLayoutStore.Save(TileLayout);
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Application list load failed: {exception}");
        }
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
        var width = Math.Min((int)Math.Round(ActualWidth * dpi / 96.0), monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left);
        var height = Math.Min((int)Math.Round(ActualHeight * dpi / 96.0), monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top);
        var left = monitorInfo.WorkArea.Left;
        var top = monitorInfo.WorkArea.Bottom - height;
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(handle, HwndTopmost, left, top, width, height, SwpShowWindow);
    }

    private static uint GetMonitorDpi(nint monitor)
    {
        return GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0 ? dpiX : 96;
    }

    private void EnableAcrylic()
    {
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

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        SaveCurrentSize();
        ClearSearch();
        Hide();
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
            if (SearchPanel.Visibility == Visibility.Visible)
            {
                ClearSearch();
            }
            else
            {
                SaveCurrentSize();
                Hide();
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

        ShowSearch();
        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        AppsView.Filter = item => item is AppEntry app && app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        RecentPanel.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppsView.Refresh();
    }

    private void ShowSearch()
    {
        SearchPanel.Visibility = Visibility.Visible;
        SearchBox.Focus();
    }

    private void ClearSearch()
    {
        SearchBox.Clear();
        SearchPanel.Visibility = Visibility.Collapsed;
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

        if (AppLauncher.Launch(app))
        {
            ClearSearch();
            Hide();
        }
    }

    private void TileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_dragCompleted)
        {
            _dragCompleted = false;
            return;
        }

        if (sender is Button { Tag: TileItem tile } && AppLauncher.Launch(tile.Name, tile.LaunchTarget))
        {
            Hide();
        }
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
        if (e.LeftButton != MouseButtonState.Pressed || _dragTile is null || _dragSource is null)
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
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(TileItem), tile), DragDropEffects.Move);
        _dragTile = null;
        _dragSource = null;
    }

    private void TileGroup_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TileItem)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void TileGroup_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not ItemsControl { Tag: TileGroup target }
            || e.Data.GetData(typeof(TileItem)) is not TileItem tile
            || _dragSource is null)
        {
            return;
        }

        var position = e.GetPosition((ItemsControl)sender);
        var column = Math.Clamp((int)Math.Round(position.X / TileItem.CellPitch), 0, TileGroup.Columns - tile.Size.ColumnSpan());
        var row = Math.Max(0, (int)Math.Round(position.Y / TileItem.CellPitch));
        if (TileLayoutEngine.Move(_dragSource, target, tile, column, row))
        {
            TileLayoutStore.Save(TileLayout);
        }

        e.Handled = true;
    }

    private static void RestoreTileIcons(TileLayout layout, IReadOnlyList<AppEntry> apps)
    {
        var icons = apps
            .GroupBy(app => app.LaunchTarget, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Icon, StringComparer.OrdinalIgnoreCase);
        foreach (var tile in layout.Groups.SelectMany(group => group.Tiles))
        {
            if (icons.TryGetValue(tile.LaunchTarget, out var icon))
            {
                tile.Icon = icon;
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

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(nint window, ref WindowCompositionAttributeData data);
}
