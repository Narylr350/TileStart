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

public partial class MainWindow
{
    private readonly StartWindowLifecycle _foregroundLifecycle = new();
    private TaskbarEdge _taskbarEdge = TaskbarEdge.Bottom;
    private readonly TileReflowStability _tileReflowStability = new();
    private bool _entranceSnapshotActive;
    private int _entranceSnapshotGeneration;
    private long _suppressTileActivationUntil;
    private bool _allowClose;
    private bool _recentAppsExpanded;
    private bool _isDismissing;
    private HwndSource? _windowSource;
    private int _openContextMenuCount;

    private void TryAcquireForeground(int generation, int attempt)
    {
        if (generation != _foregroundActivationGeneration || !IsVisible)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        var requested = handle != 0 && RequestForegroundWindow(handle);
        Activate();
        Focus();
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
            _ = Dispatcher.BeginInvoke(
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
        _navigationHoverTimer.Stop();
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
            if (IsVisible && GetForegroundWindow() is var foreground && ForegroundBelongsToStart(foreground))
            {
                _foregroundLifecycle.ObserveNativeActivation();
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
        var hasActiveOwnedWindow = OwnedWindows.Cast<Window>().Any(window => window.IsActive);
        if (_foregroundLifecycle.ObserveForeground(
                foregroundKnown,
                foregroundBelongsToStart,
                hasActiveOwnedWindow,
                _openContextMenuCount > 0))
        {
            DiagnosticLog.Write(
                $"Window dismissal: trigger={trigger}, foreground=0x{foreground.ToInt64():X}, active={IsActive}, acquired={_foregroundLifecycle.HasAcquiredForeground}, foreignSamples={_foregroundLifecycle.ForeignForegroundObservations}, ownedActive={hasActiveOwnedWindow}, contextMenus={_openContextMenuCount}.");
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
}
