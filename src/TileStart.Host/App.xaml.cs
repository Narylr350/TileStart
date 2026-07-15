using System.Windows;

namespace TileStart.Host;

public partial class App : System.Windows.Application
{
    private OpenRequestServer? _server;
    private ShellIntegrationManager? _shellIntegration;
    private SingleInstanceGuard? _singleInstance;
    private TrayIcon? _trayIcon;
    private WinKeyHook? _winKeyHook;
    private bool _isPaused;

    public App()
    {
        DispatcherUnhandledException += (_, args) => DiagnosticLog.Write($"Dispatcher exception: {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => DiagnosticLog.Write($"Unhandled exception: {args.ExceptionObject}");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DiagnosticLog.Write("Host startup started.");

        var shutdownRequested = e.Args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase);
        _singleInstance = new SingleInstanceGuard();
        if (!_singleInstance.IsPrimaryInstance)
        {
            SingleInstanceGuard.NotifyPrimaryInstance(shutdownRequested);
            Shutdown();
            return;
        }

        if (shutdownRequested)
        {
            Shutdown();
            return;
        }

        DiagnosticLog.Write("Creating main window.");
        MainWindow = new MainWindow();
        PrimeHiddenWindow(MainWindow);
        DiagnosticLog.Write("Main window created.");
        _server = new OpenRequestServer(((MainWindow)MainWindow).ShowFromShell, ExitApplication, Dispatcher);
        _server.Start();
        _winKeyHook = new WinKeyHook(() => Dispatcher.BeginInvoke(((MainWindow)MainWindow).ShowFromShell));
        if (!_winKeyHook.Start())
        {
            DiagnosticLog.Write("Win-key hook could not be installed; native Win-key behavior remains active.");
        }

        _shellIntegration = new ShellIntegrationManager();
        if (!_shellIntegration.Start())
        {
            DiagnosticLog.Write("Shell integration could not be started; native Start-button behavior remains active.");
        }
        _trayIcon = new TrayIcon(((MainWindow)MainWindow).ShowFromShell,
                                 SetPaused,
                                 WinKeyHook.OpenNativeStartMenu,
                                 ExitApplication);
        DiagnosticLog.Write("Host startup completed.");
    }

    private static void PrimeHiddenWindow(Window window)
    {
        var left = window.Left;
        var top = window.Top;
        var opacity = window.Opacity;
        var showActivated = window.ShowActivated;
        window.Left = -32000;
        window.Top = -32000;
        window.Opacity = 0;
        window.ShowActivated = false;
        window.Show();
        window.Hide();
        window.Left = left;
        window.Top = top;
        window.Opacity = opacity;
        window.ShowActivated = showActivated;
    }

    private void SetPaused(bool paused)
    {
        if (_isPaused == paused)
        {
            return;
        }

        _isPaused = paused;
        if (paused)
        {
            _winKeyHook?.Dispose();
            _shellIntegration?.Stop();
        }
        else
        {
            if (_winKeyHook?.Start() == false)
            {
                DiagnosticLog.Write("Win-key hook could not be resumed.");
            }

            if (_shellIntegration?.Start() == false)
            {
                DiagnosticLog.Write("Shell integration could not be resumed.");
            }
        }
    }

    private void ExitApplication()
    {
        ((MainWindow)MainWindow).AllowClose();
        Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _winKeyHook?.Dispose();
        if (_server is not null)
        {
            await _server.StopAsync();
        }
        _shellIntegration?.Dispose();
        _singleInstance?.Dispose();

        base.OnExit(e);
    }
}
