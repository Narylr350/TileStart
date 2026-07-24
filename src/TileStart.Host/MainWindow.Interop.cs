using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Windowing;

namespace TileStart.Host;

public partial class MainWindow
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint SwpNoActivate = 0x0010;
    private const uint AwHide = 0x00010000;
    private const uint AwBlend = 0x00080000;
    private const int DismissDurationMilliseconds = 150;
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
    private const int GwOwner = 4;
    private static readonly nint HwndTopmost = new(-1);

    private bool _isWindowWidthSnapAnimating;
    private long _windowWidthSnapStartedAt;
    private double _windowWidthSnapFrom;
    private double _windowWidthSnapTo;
    private double _windowWidthSnapRight;
    private IReadOnlyDictionary<TileGroup, System.Windows.Point>? _windowWidthSnapGroupPositions;

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
    private static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint firstThreadId, uint secondThreadId, bool attach);

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
