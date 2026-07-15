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
        DiagnosticLog.Write("Main window created.");
        _server = new OpenRequestServer(((MainWindow)MainWindow).ShowFromShell, ExitApplication, Dispatcher);
        _server.Start();
        _winKeyHook = new WinKeyHook(() => Dispatcher.BeginInvoke(((MainWindow)MainWindow).ShowFromShell));
        _winKeyHook.Start();
        _shellIntegration = new ShellIntegrationManager();
        _shellIntegration.Start();
        _trayIcon = new TrayIcon(((MainWindow)MainWindow).ShowFromShell,
                                 SetPaused,
                                 WinKeyHook.OpenNativeStartMenu,
                                 ExitApplication);
        DiagnosticLog.Write("Host startup completed.");
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
            _winKeyHook?.Start();
            _shellIntegration?.Start();
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
