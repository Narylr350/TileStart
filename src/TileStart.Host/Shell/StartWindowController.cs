using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Themes;
using TileStart.Host.Utilities;
using TileStart.Host.Windowing;

namespace TileStart.Host.Shell;

public class StartWindowController : IDisposable
{
    private const int WmDisplayChange = 0x007E;
    private const int WmSettingChange = 0x001A;
    private const int WmDpiChanged = 0x02E0;
    private const int SpiSetWorkArea = 0x002F;
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint AwHide = 0x00010000;
    private const uint AwBlend = 0x00080000;
    private const int DismissDurationMilliseconds = 150;
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerDoNotRound = 1;
    private const int DwmWindowCornerRound = 2;
    private const int WmActivate = 0x0006;
    private const int WaInactive = 0;
    private const int WmNcLButtonDown = 0x00A1;
    private const int WmEnterSizeMove = 0x0231;
    private const int WmExitSizeMove = 0x0232;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopRight = 14;
    private const int GwOwner = 4;
    private static readonly nint HwndTopmost = new(-1);

    private readonly Window _window;
    private readonly Grid _windowRoot;
    private readonly Grid _mainSurface;
    private readonly Action _beforeShow;
    private readonly Action _clearSearch;
    private readonly Func<bool> _ensureTileScrollBarClearance;
    private readonly Func<IReadOnlyDictionary<TileGroup, System.Windows.Point>> _captureGroupReorderPositions;
    private readonly Action<IReadOnlyDictionary<TileGroup, System.Windows.Point>> _animateGroupReorderFrom;
    private readonly Func<bool> _isAnyDragActive;
    private readonly Func<bool> _hasOpenContextMenu;
    private readonly Action _cancelActiveDrag;
    private readonly AppThemeStyle _themeStyle;

    private readonly System.Windows.Threading.DispatcherTimer _foregroundWatchdogTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(50),
    };

    private readonly System.Windows.Threading.DispatcherTimer _displayChangeRefreshTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250),
    };

    private readonly StartWindowLifecycle _foregroundLifecycle = new();
    private TaskbarEdge _taskbarEdge = TaskbarEdge.Bottom;
    private int _foregroundActivationGeneration;
    private bool _allowClose;
    private bool _isDismissing;
    private HwndSource? _windowSource;
    private bool _isDisposed;

    private bool _isDisplayChangePending;
    private bool _isLiveResizeActive;
    private bool _restoreMaterialAfterLiveResize;
    private nint _pendingDisplayMonitor;
    private double _requestedMinimumWidth = StartWindowSizing.WidthForColumns(0);
    private readonly double _requestedMinimumHeight;
    private int _preferredWorkspaceColumns;
    private double _preferredHeight;

    public event Action? WindowDismissing;
    public event Action? WindowShown;

    public TaskbarEdge TaskbarEdge => _taskbarEdge;
    public bool IsWindowVisible => _window.IsVisible;

    public StartWindowController(
        Window window,
        Grid windowRoot,
        Grid mainSurface,
        Action beforeShow,
        Action clearSearch,
        Func<bool> ensureTileScrollBarClearance,
        Func<IReadOnlyDictionary<TileGroup, System.Windows.Point>> captureGroupReorderPositions,
        Action<IReadOnlyDictionary<TileGroup, System.Windows.Point>> animateGroupReorderFrom,
        Func<bool> isAnyDragActive,
        Func<bool> hasOpenContextMenu,
        Action cancelActiveDrag,
        int preferredWorkspaceColumns,
        double preferredHeight,
        AppThemeStyle themeStyle = AppThemeStyle.Windows11)
    {
        _window = window;
        _windowRoot = windowRoot;
        _mainSurface = mainSurface;
        _beforeShow = beforeShow;
        _clearSearch = clearSearch;
        _ensureTileScrollBarClearance = ensureTileScrollBarClearance;
        _captureGroupReorderPositions = captureGroupReorderPositions;
        _animateGroupReorderFrom = animateGroupReorderFrom;
        _isAnyDragActive = isAnyDragActive;
        _hasOpenContextMenu = hasOpenContextMenu;
        _cancelActiveDrag = cancelActiveDrag;
        _requestedMinimumHeight = window.MinHeight;
        _preferredWorkspaceColumns = Math.Clamp(
            preferredWorkspaceColumns,
            StartWindowSizing.MinimumGroupColumns,
            StartWindowSizing.MaximumGroupColumns);
        _preferredHeight = double.IsFinite(preferredHeight) && preferredHeight > 0
            ? preferredHeight
            : window.Height;
        _themeStyle = themeStyle;

        _foregroundWatchdogTimer.Tick += ForegroundWatchdogTimer_Tick;
        _displayChangeRefreshTimer.Tick += DisplayChangeRefreshTimer_Tick;
    }

    public void SetMinimumWorkspaceColumns(int columns)
    {
        _requestedMinimumWidth = StartWindowSizing.WidthForColumns(columns);
        _window.MinWidth = Math.Min(_requestedMinimumWidth, _window.MaxWidth);
        var currentWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
        if (currentWidth + 0.5 >= _window.MinWidth)
        {
            return;
        }

        _window.Width = _window.MinWidth;
        PositionOnCurrentMonitor();
    }

    public void SetWindowSource(HwndSource? source)
    {
        if (_windowSource != null)
            _windowSource.RemoveHook(WindowMessageHook);
        _windowSource = _isDisposed ? null : source;
        _windowSource?.AddHook(WindowMessageHook);
    }

    public void ShowFromShell()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_window.IsVisible)
        {
            DismissWindow();
            return;
        }

        InteractiveProcessPriority.Boost();
        _beforeShow();

        StopEntranceCache();
        _foregroundLifecycle.Reset();
        PositionOnCurrentMonitor();
        ApplyWindowMaterial();
        PrepareMotionElements();
        var animationsEnabled = SystemParameters.ClientAreaAnimation;
        _ = RenderFrameProbe.Start(
            _window.Dispatcher,
            "start-entrance",
            TimeSpan.FromMilliseconds(650));
        if (animationsEnabled)
        {
            _mainSurface.CacheMode = new BitmapCache { RenderAtScale = 1 };
        }

        StartMotion.StageEntrance(
            _mainSurface,
            [_mainSurface],
            _taskbarEdge == TaskbarEdge.Bottom,
            animationsEnabled);
        _window.Show();
        _ = _window.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            () =>
            {
                if (_ensureTileScrollBarClearance())
                {
                    PositionOnCurrentMonitor();
                }
            });
        _window.Activate();
        _window.Focus();
        var activationGeneration = ++_foregroundActivationGeneration;
        TryAcquireForeground(activationGeneration, 0);

        _foregroundWatchdogTimer.Start();
        StartMotion.PlayEntrance(
            [_mainSurface],
            animationsEnabled,
            StopEntranceCache);
        WindowShown?.Invoke();
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    public void OnClosing(CancelEventArgs e)
    {
        PersistPreferredSize();
        if (!_allowClose)
        {
            e.Cancel = true;
            _foregroundWatchdogTimer.Stop();
            _window.Hide();
            InteractiveProcessPriority.Restore();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        InteractiveProcessPriority.Restore();
        _foregroundActivationGeneration++;
        StopEntranceCache();
        _foregroundWatchdogTimer.Stop();
        _foregroundWatchdogTimer.Tick -= ForegroundWatchdogTimer_Tick;
        _displayChangeRefreshTimer.Stop();
        _displayChangeRefreshTimer.Tick -= DisplayChangeRefreshTimer_Tick;
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        WindowDismissing = null;
        WindowShown = null;
    }

    public void ApplyWindowMaterial()
    {
        if (PresentationSource.FromVisual(_window) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        ApplyWindowCornerPreference();
        var material = Win10Theme.ReadStartMaterial(_themeStyle);
        var acrylicApplied = SetAccentPolicy(
            material.UseAcrylic ? AccentEnableAcrylicBlurBehind : 0,
            material.UseAcrylic ? 2 : 0,
            material.AcrylicGradientColor);
        _mainSurface.Background = material.UseAcrylic && acrylicApplied
            ? System.Windows.Media.Brushes.Transparent
            : new SolidColorBrush(material.FallbackColor);
        _restoreMaterialAfterLiveResize = false;
    }

    private void ApplyWindowCornerPreference()
    {
        var preference = _themeStyle == AppThemeStyle.Windows11
            ? DwmWindowCornerRound
            : DwmWindowCornerDoNotRound;
        _ = DwmSetWindowAttribute(
            new WindowInteropHelper(_window).Handle,
            DwmWindowCornerPreference,
            ref preference,
            sizeof(int));
    }

    public void WindowDeactivated()
    {
        _foregroundLifecycle.ObserveNativeDeactivation();
        RequestDismissAfterForegroundChange("deactivated");
    }

    public void TopResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginResize(HtTop, e);
    }

    public void RightResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginResize(HtRight, e);
    }

    public void TopRightResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginResize(HtTopRight, e);
    }

    private void BeginResize(int hitTest, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Mouse.Capture(null);
        SendMessage(new WindowInteropHelper(_window).Handle, WmNcLButtonDown, hitTest, 0);
        if (hitTest is HtRight or HtTopRight)
        {
            SnapWindowWidthAfterResize();
        }
        else
        {
            CapturePreferredSize();
        }
    }

    private void TryAcquireForeground(int generation, int attempt)
    {
        if (_isDisposed || generation != _foregroundActivationGeneration || !_window.IsVisible)
        {
            return;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        var requested = handle != 0 && RequestForegroundWindow(handle);
        _window.Activate();
        _window.Focus();
        var foreground = GetForegroundWindow();
        var acquired = foreground != 0 && ForegroundBelongsToStart(foreground);
        _foregroundLifecycle.ObserveForeground(
            foreground != 0,
            acquired,
            hasActiveOwnedWindow: false,
            hasOpenContextMenu: false);
        DiagnosticLog.Write(
            $"Window foreground activation: attempt={attempt}, requested={requested}, foreground=0x{foreground.ToInt64():X}, acquired={acquired}.");

        if (!acquired && attempt < 4)
        {
            _ = _window.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                () => TryAcquireForeground(generation, attempt + 1));
        }
    }

    private static bool RequestForegroundWindow(nint window)
    {
        var foreground = GetForegroundWindow();
        var currentThread = GetCurrentThreadId();
        var foregroundThread = foreground == 0 ? 0 : GetWindowThreadProcessId(foreground, out _);
        var attached = foregroundThread != 0
                       && foregroundThread != currentThread
                       && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            BringWindowToTop(window);
            return SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    public void PrepareMotionElements()
    {
        if (_window.IsVisible)
        {
            return;
        }

        if (!_windowRoot.IsMeasureValid || !_windowRoot.IsArrangeValid
                                        || _windowRoot.ActualWidth <= 0 || _windowRoot.ActualHeight <= 0)
        {
            var size = new System.Windows.Size(Math.Max(_window.MinWidth, _window.Width),
                Math.Max(_window.MinHeight, _window.Height));
            _windowRoot.Measure(size);
            _windowRoot.Arrange(new System.Windows.Rect(new System.Windows.Point(), size));
            _windowRoot.UpdateLayout();
        }

        StartMotion.Prepare([_mainSurface]);
    }

    private void StopEntranceCache()
    {
        _mainSurface.CacheMode = null;
    }

    private void PositionOnCurrentMonitor() => PositionOnMonitor(GetCursorMonitor());

    private nint GetCursorMonitor()
    {
        if (!GetCursorPos(out var cursor))
        {
            return 0;
        }

        return MonitorFromPoint(cursor, MonitorDefaultToNearest);
    }

    private void PositionOnMonitor(nint monitor)
    {
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
        _window.MinWidth = Math.Min(_requestedMinimumWidth, logicalWorkWidth);
        _window.MinHeight = Math.Min(_requestedMinimumHeight, logicalWorkHeight);
        _window.MaxWidth = StartWindowSizing.MaximumWidth(logicalWorkWidth);
        _window.MaxHeight = Math.Max(1, logicalWorkHeight);
        var requestedWidth = StartWindowSizing.WidthForColumns(_preferredWorkspaceColumns);
        var logicalWidth = Math.Max(
            _window.MinWidth,
            StartWindowSizing.SnapWidth(requestedWidth, logicalWorkWidth));
        var logicalHeight = StartWindowSizing.ClampHeight(
            _preferredHeight,
            _window.MinHeight,
            logicalWorkHeight);
        var placement = StartWindowPlacement.Calculate(
            workArea,
            edge,
            (int)Math.Round(logicalWidth * dpi / 96.0),
            (int)Math.Round(logicalHeight * dpi / 96.0));
        // WPF needs the logical values before the first Show; SetWindowPos below remains
        // the physical-pixel authority after the HWND exists.
        _window.Left = placement.Left * scale;
        _window.Top = placement.Top * scale;
        _window.Width = placement.Width * scale;
        _window.Height = placement.Height * scale;

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == 0)
        {
            return;
        }

        var positioned = SetWindowPos(handle, HwndTopmost, placement.Left, placement.Top, placement.Width,
            placement.Height, SwpNoActivate | SwpFrameChanged);
        DiagnosticLog.Write(
            $"Window placement: monitor={monitorRect}, work={ToPixelRect(monitorInfo.WorkArea)}, dpi={dpi}, " +
            $"logicalWork={logicalWorkWidth:F1}x{logicalWorkHeight:F1}, min={_window.MinWidth:F1}, " +
            $"max={_window.MaxWidth:F1}, preferredColumns={_preferredWorkspaceColumns}, taskbar={taskbarRect}, " +
            $"edge={edge}, target={placement}, positioned={positioned}, " +
            $"error={(positioned ? 0 : Marshal.GetLastWin32Error())}.");
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
            return SetWindowCompositionAttribute(new WindowInteropHelper(_window).Handle, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPointer);
        }
    }

    private void CapturePreferredSize()
    {
        if (_isDisplayChangePending)
        {
            return;
        }

        if (_window.ActualWidth > 0 && _window.ActualHeight > 0)
        {
            _preferredWorkspaceColumns = StartWindowSizing.ColumnsForWidth(_window.ActualWidth, _window.MaxWidth);
            _preferredHeight = _window.ActualHeight;
            PersistPreferredSize();
        }
    }

    private void PersistPreferredSize() =>
        WindowSizeStore.Save(_preferredWorkspaceColumns, _preferredHeight);

    public void DismissWindow(bool yieldTopmost = false)
    {
        if (!_window.IsVisible)
        {
            return;
        }

        if (_isDismissing)
        {
            if (yieldTopmost)
            {
                _window.Topmost = false;
            }

            return;
        }

        _isDismissing = true;
        WindowDismissing?.Invoke();
        _foregroundWatchdogTimer.Stop();
        StopEntranceCache();
        PersistPreferredSize();

        _clearSearch();
        var wasTopmost = _window.Topmost;
        if (yieldTopmost)
        {
            _window.Topmost = false;
        }

        try
        {
            var handle = new WindowInteropHelper(_window).Handle;
            if (SystemParameters.ClientAreaAnimation && handle != 0)
            {
                AnimateWindow(handle, DismissDurationMilliseconds, AwHide | AwBlend);
            }

            _window.Hide();
        }
        finally
        {
            _window.Topmost = wasTopmost;
            _isDismissing = false;
            InteractiveProcessPriority.Restore();
        }
    }

    private nint WindowMessageHook(
        nint window,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (_isDisposed)
        {
            return 0;
        }

        if (message == WmEnterSizeMove)
        {
            BeginLiveResize();
            return 0;
        }

        if (message == WmExitSizeMove)
        {
            EndLiveResize();
            return 0;
        }

        if (message == WmDpiChanged)
        {
            HandleDpiChanged(window, lParam);
            return 0;
        }

        if (message == WmDisplayChange
            || message == WmSettingChange && wParam.ToInt64() == SpiSetWorkArea)
        {
            RequestDisplayChangeRefresh(0);
            return 0;
        }

        if (message != WmActivate)
        {
            return 0;
        }

        if ((wParam.ToInt64() & 0xffff) != WaInactive)
        {
            if (_window.IsVisible && GetForegroundWindow() is var foreground && ForegroundBelongsToStart(foreground))
            {
                _foregroundLifecycle.ObserveNativeActivation();
            }

            return 0;
        }

        _foregroundLifecycle.ObserveNativeDeactivation();
        RequestDismissAfterForegroundChange("wm-activate");
        return 0;
    }

    private void RequestDismissAfterForegroundChange(string trigger)
    {
        _window.Dispatcher.BeginInvoke(
            () =>
            {
                if (!_isDisposed)
                {
                    TryDismissAfterForegroundChange(trigger);
                }
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ForegroundWatchdogTimer_Tick(object? sender, EventArgs e)
    {
        if (!_window.IsVisible)
        {
            _foregroundWatchdogTimer.Stop();
            return;
        }

        if (!IsAnyMouseButtonPressed() && !_isAnyDragActive())
        {
            TryDismissAfterForegroundChange("foreground-watchdog");
        }
    }

    public void TryDismissAfterForegroundChange(string trigger)
    {
        if (_isDisposed
            || !_window.IsVisible
            || _isDismissing
            || IsAnyMouseButtonPressed()
            || _isAnyDragActive())
        {
            return;
        }

        var foreground = GetForegroundWindow();
        var foregroundKnown = foreground != 0;
        var foregroundBelongsToStart = foregroundKnown && ForegroundBelongsToStart(foreground);
        var hasActiveOwnedWindow = _window.OwnedWindows.Cast<Window>().Any(window => window.IsActive);
        if (_foregroundLifecycle.ObserveForeground(
                foregroundKnown,
                foregroundBelongsToStart,
                hasActiveOwnedWindow,
                _hasOpenContextMenu()))
        {
            DiagnosticLog.Write(
                $"Window dismissal: trigger={trigger}, foreground=0x{foreground.ToInt64():X}, active={_window.IsActive}, acquired={_foregroundLifecycle.HasAcquiredForeground}, foreignSamples={_foregroundLifecycle.ForeignForegroundObservations}, ownedActive={hasActiveOwnedWindow}, contextMenus={_hasOpenContextMenu()}.");
            DismissWindow(yieldTopmost: true);
        }
    }

    private bool ForegroundBelongsToStart(nint foreground)
    {
        var mainWindow = new WindowInteropHelper(_window).Handle;
        if (foreground == 0 || mainWindow == 0)
        {
            return _window.IsActive;
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

    private void SnapWindowWidthAfterResize()
    {
        var currentWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
        var targetWidth = StartWindowSizing.SnapWidth(currentWidth, _window.MaxWidth);
        var calculatedColumns = StartWindowSizing.ColumnsForWidth(targetWidth, _window.MaxWidth);
        var previousGroupPositions = _captureGroupReorderPositions();
        // Changing Width on every render frame forces the complete tile workspace through
        // repeated WPF measure/arrange passes. That looks acceptable on a fast GPU but
        // becomes delayed under software rendering, remote sessions, and slower machines.
        // Snap the native window once; only the lightweight group transforms animate.
        // PositionOnCurrentMonitor treats the preferred column count as the source of truth,
        // so publish the newly selected count before it recalculates the window placement.
        // Reversing this order immediately restores the old width and makes expansion appear
        // impossible even though the pointer reached the wider column boundary.
        _preferredWorkspaceColumns = calculatedColumns;
        _window.Width = targetWidth;
        PositionOnCurrentMonitor();
        _window.UpdateLayout();
        _animateGroupReorderFrom(previousGroupPositions);
        DiagnosticLog.Write(
            $"Window resize snap: current={currentWidth:F1}, target={targetWidth:F1}, max={_window.MaxWidth:F1}, " +
            $"calculatedColumns={calculatedColumns}, preferredColumns={_preferredWorkspaceColumns}.");
        CapturePreferredSize();
    }

    private void BeginLiveResize()
    {
        if (_isLiveResizeActive)
        {
            return;
        }

        _isLiveResizeActive = true;
        _restoreMaterialAfterLiveResize = true;
        // Acrylic composition is substantially more expensive during native live resize,
        // especially on integrated GPUs and software-rendered sessions. Disable only the
        // blur effect while the pointer is moving, but retain the wallpaper-derived Start
        // surface color so the menu does not visibly lose the user's theme. Restore the full
        // material once the final snapped layout has settled.
        SetAccentPolicy(0, 0, 0);
        _mainSurface.Background = new SolidColorBrush(Win10Theme.StartSurfaceColor);
        DiagnosticLog.Write(
            $"Window live resize begin: width={_window.ActualWidth:F1}, min={_window.MinWidth:F1}, " +
            $"max={_window.MaxWidth:F1}, renderTier={RenderCapability.Tier >> 16}.");
    }

    private void EndLiveResize()
    {
        if (!_isLiveResizeActive)
        {
            return;
        }

        _isLiveResizeActive = false;
        DiagnosticLog.Write($"Window live resize end: width={_window.ActualWidth:F1}.");
        _ = _window.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Render,
            () =>
            {
                if (!_isDisposed && !_isLiveResizeActive && _restoreMaterialAfterLiveResize)
                {
                    ApplyWindowMaterial();
                }
            });
    }

    private void HandleDpiChanged(nint window, nint suggestedRectPointer)
    {
        _cancelActiveDrag();
        if (suggestedRectPointer == 0)
        {
            RequestDisplayChangeRefresh(0);
            return;
        }

        var suggested = Marshal.PtrToStructure<Rect>(suggestedRectPointer);
        SetWindowPos(
            window,
            HwndTopmost,
            suggested.Left,
            suggested.Top,
            Math.Max(1, suggested.Right - suggested.Left),
            Math.Max(1, suggested.Bottom - suggested.Top),
            SwpNoActivate | SwpFrameChanged);
        RequestDisplayChangeRefresh(MonitorFromRect(ref suggested, MonitorDefaultToNearest));
    }

    private void RequestDisplayChangeRefresh(nint monitor)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisplayChangePending = true;
        _cancelActiveDrag();
        _pendingDisplayMonitor = monitor;

        _displayChangeRefreshTimer.Stop();
        _displayChangeRefreshTimer.Start();
    }

    private void DisplayChangeRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _displayChangeRefreshTimer.Stop();
        if (_isDisposed)
        {
            _isDisplayChangePending = false;
            return;
        }

        if (_window.IsVisible)
        {
            var monitor = _pendingDisplayMonitor != 0 ? _pendingDisplayMonitor : GetCursorMonitor();
            PositionOnMonitor(monitor);
            _window.UpdateLayout();
            ApplyWindowMaterial();
        }

        _pendingDisplayMonitor = 0;
        _isDisplayChangePending = false;
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
    private static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref Rect rect, uint flags);

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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int valueSize);
}
