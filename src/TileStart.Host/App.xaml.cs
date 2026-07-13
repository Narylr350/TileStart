using System.Windows;

namespace TileStart.Host;

public partial class App : Application
{
    private OpenRequestServer? _server;
    private ShellIntegrationManager? _shellIntegration;
    private WinKeyHook? _winKeyHook;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow = new MainWindow();
        _server = new OpenRequestServer((MainWindow)MainWindow, Dispatcher);
        _server.Start();
        _winKeyHook = new WinKeyHook(() => Dispatcher.BeginInvoke(((MainWindow)MainWindow).ShowFromShell));
        _winKeyHook.Start();
        _shellIntegration = new ShellIntegrationManager();
        _shellIntegration.Start();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _winKeyHook?.Dispose();
        if (_server is not null)
        {
            await _server.StopAsync();
        }
        _shellIntegration?.Dispose();

        base.OnExit(e);
    }
}
