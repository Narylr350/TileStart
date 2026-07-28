using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TileStart.Host.Windowing;

public static class DialogWindowManager
{
    private static readonly ConditionalWeakTable<Window, DialogWindowState> States = new();

    public static void Attach(Window window) =>
        States.GetValue(window, static value => new DialogWindowState(value)).Attach();

    private sealed class DialogWindowState
    {
        private const int WmDisplayChange = 0x007E;
        private const int WmSettingChange = 0x001A;
        private const int WmDpiChanged = 0x02E0;
        private const int SpiSetWorkArea = 0x002F;
        private const uint MonitorDefaultToNearest = 2;
        private const int MdtEffectiveDpi = 0;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpNoZOrder = 0x0004;

        private readonly Window _window;
        private HwndSource? _source;
        private LogicalWindowSize _desiredSize;
        private LogicalWindowSize _minimumSize;
        private bool _attached;
        private bool _displayChangePending;
        private bool _positioning;

        public DialogWindowState(Window window)
        {
            _window = window;
        }

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            _attached = true;
            _desiredSize = new LogicalWindowSize(
                ResolveDimension(_window.Width, _window.ActualWidth),
                ResolveDimension(_window.Height, _window.ActualHeight));
            _minimumSize = new LogicalWindowSize(_window.MinWidth, _window.MinHeight);
            _window.SizeToContent = SizeToContent.Manual;
            _source = PresentationSource.FromVisual(_window) as HwndSource;
            _source?.AddHook(WindowMessageHook);
            _window.Closed += Window_Closed;
            _window.SizeChanged += Window_SizeChanged;
            FitAndPosition(centerOnOwner: true);
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _source?.RemoveHook(WindowMessageHook);
            _source = null;
            _window.Closed -= Window_Closed;
            _window.SizeChanged -= Window_SizeChanged;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_positioning || _displayChangePending || !_window.IsVisible)
            {
                return;
            }

            _desiredSize = new LogicalWindowSize(
                Math.Max(1, _window.ActualWidth),
                Math.Max(1, _window.ActualHeight));
        }

        private nint WindowMessageHook(
            nint window,
            int message,
            nint wParam,
            nint lParam,
            ref bool handled)
        {
            if (message == WmDpiChanged && lParam != 0)
            {
                var suggested = Marshal.PtrToStructure<NativeRect>(lParam);
                SetWindowPos(
                    window,
                    0,
                    suggested.Left,
                    suggested.Top,
                    suggested.Width,
                    suggested.Height,
                    SwpNoActivate | SwpNoZOrder);
                ScheduleFit();
            }
            else if (message == WmDisplayChange
                     || message == WmSettingChange && wParam.ToInt64() == SpiSetWorkArea)
            {
                ScheduleFit();
            }

            // WPF must continue processing WM_DPICHANGED so its visual tree adopts the new scale.
            return 0;
        }

        private void ScheduleFit()
        {
            if (_displayChangePending)
            {
                return;
            }

            _displayChangePending = true;
            _window.Dispatcher.BeginInvoke(
                () =>
                {
                    try
                    {
                        FitAndPosition(centerOnOwner: false);
                    }
                    finally
                    {
                        _displayChangePending = false;
                    }
                },
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void FitAndPosition(bool centerOnOwner)
        {
            if (_positioning)
            {
                return;
            }

            var handle = new WindowInteropHelper(_window).Handle;
            if (handle == 0)
            {
                return;
            }

            _positioning = true;
            try
            {
                var ownerHandle = centerOnOwner && _window.Owner is { } owner
                    ? new WindowInteropHelper(owner).Handle
                    : 0;
                var monitor = MonitorFromWindow(ownerHandle != 0 ? ownerHandle : handle, MonitorDefaultToNearest);
                var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (monitor == 0 || !GetMonitorInfoW(monitor, ref monitorInfo))
                {
                    return;
                }

                var dpi = GetMonitorDpi(monitor);
                var scale = 96.0 / dpi;
                var workArea = ToPixelRect(monitorInfo.WorkArea);
                var logicalWorkArea = new LogicalWindowSize(workArea.Width * scale, workArea.Height * scale);
                var fitted = WindowWorkAreaLayout.FitSize(_desiredSize, _minimumSize, logicalWorkArea);
                var available = new LogicalWindowSize(
                    Math.Max(1, logicalWorkArea.Width - WindowWorkAreaLayout.SafeInset * 2),
                    Math.Max(1, logicalWorkArea.Height - WindowWorkAreaLayout.SafeInset * 2));

                _window.MinWidth = Math.Min(_minimumSize.Width, fitted.Width);
                _window.MinHeight = Math.Min(_minimumSize.Height, fitted.Height);
                _window.MaxWidth = available.Width;
                _window.MaxHeight = available.Height;
                _window.Width = fitted.Width;
                _window.Height = fitted.Height;
                _window.UpdateLayout();

                PixelRect? ownerRect = null;
                if (ownerHandle != 0 && GetWindowRect(ownerHandle, out var nativeOwnerRect))
                {
                    ownerRect = ToPixelRect(nativeOwnerRect);
                }

                var placement = WindowWorkAreaLayout.CenterAndClamp(
                    workArea,
                    ownerRect,
                    (int)Math.Round(fitted.Width * dpi / 96.0),
                    (int)Math.Round(fitted.Height * dpi / 96.0));
                SetWindowPos(
                    handle,
                    0,
                    placement.Left,
                    placement.Top,
                    placement.Width,
                    placement.Height,
                    SwpNoActivate | SwpNoZOrder);
            }
            finally
            {
                _positioning = false;
            }
        }

        private static uint GetMonitorDpi(nint monitor) =>
            GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0 ? dpiX : 96;

        private static double ResolveDimension(double requested, double actual) =>
            double.IsFinite(requested) && requested > 0 ? requested : Math.Max(1, actual);

        private static PixelRect ToPixelRect(NativeRect rect) =>
            new(rect.Left, rect.Top, rect.Right, rect.Bottom);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public readonly int Width => Math.Max(1, Right - Left);
            public readonly int Height => Math.Max(1, Bottom - Top);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern nint MonitorFromWindow(nint window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo monitorInfo);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(nint window, out NativeRect rect);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            nint window,
            nint insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
