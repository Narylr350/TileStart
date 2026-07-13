using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TileStart.Host;

public partial class MainWindow : Window
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint SwpShowWindow = 0x0040;
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private static readonly nint HwndTopmost = new(-1);
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        var savedSize = WindowSizeStore.Load();
        if (savedSize is not null)
        {
            Width = Math.Max(MinWidth, savedSize.Value.Width);
            Height = Math.Max(MinHeight, savedSize.Value.Height);
        }
    }

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
        Hide();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SaveCurrentSize();
            Hide();
            e.Handled = true;
        }
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
    private static extern int SetWindowCompositionAttribute(nint window, ref WindowCompositionAttributeData data);
}
