using System.Windows;

namespace TileStart.Host;

public partial class App : Application
{
    private OpenRequestServer? _server;
    private WinKeyHook? _winKeyHook;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow = new MainWindow();
        _winKeyHook = new WinKeyHook(() => Dispatcher.BeginInvoke(((MainWindow)MainWindow).ShowFromShell));
        _winKeyHook.Start();
        _server = new OpenRequestServer((MainWindow)MainWindow, Dispatcher);
        _server.Start();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _winKeyHook?.Dispose();
        if (_server is not null)
        {
            await _server.StopAsync();
        }

        base.OnExit(e);
    }
}
