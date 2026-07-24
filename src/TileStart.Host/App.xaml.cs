using System.Diagnostics;
using System.Windows;
using TileStart.Host.Backup;
using TileStart.Host.Shell;
using TileStart.Host.Updates;
using TileStart.Host.Utilities;
using MessageBox = System.Windows.MessageBox;

namespace TileStart.Host;

public partial class App : System.Windows.Application
{
    private OpenRequestServer? _server;
    private ShellIntegrationManager? _shellIntegration;
    private SingleInstanceGuard? _singleInstance;
    private TrayIcon? _trayIcon;
    private WinKeyHook? _winKeyHook;
    private BackupRestoreRequest? _pendingRestore;
    private readonly GitHubUpdateService _updateService = new();
    private bool _isPaused;
    private bool _isCheckingForUpdates;

    public App()
    {
        DispatcherUnhandledException += (_, args) => DiagnosticLog.Write($"Dispatcher exception: {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException +=
            (_, args) => DiagnosticLog.Write($"Unhandled exception: {args.ExceptionObject}");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DiagnosticLog.Write("Host startup started.");

        var startupRequest = HostRequest.FromArguments(e.Args);
        var shutdownRequested = startupRequest.Kind == HostRequestKind.Exit;
        _singleInstance = new SingleInstanceGuard();
        if (!_singleInstance.IsPrimaryInstance)
        {
            SingleInstanceGuard.NotifyPrimaryInstance(startupRequest);
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
        _server = new OpenRequestServer(HandleHostRequest, Dispatcher);
        _server.Start();
        ExplorerContextMenuRegistration.EnsureRegistered();
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
            CheckForUpdatesAsync,
            OpenBackupAndRestore,
            ExitApplication);
        if (e.Args.Length > 0 && startupRequest.Kind is not HostRequestKind.Exit and not HostRequestKind.Open)
        {
            Dispatcher.BeginInvoke(() => HandleHostRequest(startupRequest));
        }

        DiagnosticLog.Write("Host startup completed.");
    }

    private void HandleHostRequest(HostRequest request)
    {
        switch (request.Kind)
        {
            case HostRequestKind.Open:
                ((MainWindow)MainWindow).ShowFromShell();
                break;
            case HostRequestKind.Exit:
                ExitApplication();
                break;
            case HostRequestKind.AddToAppList:
            case HostRequestKind.PinTile:
                ((MainWindow)MainWindow).HandleHostRequest(request);
                break;
        }
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

    private void OpenBackupAndRestore()
    {
        Dispatcher.BeginInvoke(() =>
        {
            var dialog = new BackupRestoreWindow(ScheduleRestore);
            if (MainWindow?.IsVisible == true)
            {
                dialog.Owner = MainWindow;
            }

            dialog.ShowDialog();
        });
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isCheckingForUpdates)
        {
            MessageBox.Show("正在检查更新，请稍候。", "TileStart", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _isCheckingForUpdates = true;
        try
        {
            using var checkTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var release = await _updateService.GetLatestReleaseAsync(checkTimeout.Token);
            var currentVersion = GitHubUpdateService.CurrentVersion;
            if (!GitHubUpdateService.IsNewer(currentVersion, release.Version))
            {
                MessageBox.Show($"当前版本 {currentVersion.ToString(3)} 已是最新版本。", "TileStart",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var installedCopy = GitHubUpdateService.IsInstalledCopy(Environment.ProcessPath);
            var packageDescription = installedCopy ? "安装器" : "便携版压缩包";
            var answer = MessageBox.Show(
                $"发现新版本 {release.Version.ToString(3)}（当前 {currentVersion.ToString(3)}）。\n\n是否从 GitHub 下载并校验{packageDescription}？",
                "TileStart 更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            MessageBox.Show("更新包将在后台下载，完成 SHA-256 校验后继续。", "TileStart 更新",
                MessageBoxButton.OK, MessageBoxImage.Information);
            using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var update = await _updateService.DownloadAsync(release, installedCopy, downloadTimeout.Token);
            if (update.Kind == UpdatePackageKind.Installer)
            {
                Process.Start(new ProcessStartInfo(update.Path) { UseShellExecute = true });
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{update.Path}\"")
            {
                UseShellExecute = true,
            });
            MessageBox.Show("便携版已下载并通过校验。请退出 TileStart 后解压覆盖旧文件。", "TileStart 更新",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("检查或下载更新超时，请稍后重试。", "TileStart 更新", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Update check failed: {exception}");
            MessageBox.Show($"无法完成更新：{exception.Message}", "TileStart 更新", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private void ScheduleRestore(BackupRestoreRequest request)
    {
        _pendingRestore = request;
        ((MainWindow)MainWindow).AllowClose();
        Shutdown();
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

        Exception? restoreError = null;
        if (_pendingRestore is { } request)
        {
            try
            {
                var safetyBackup = TileStartBackupService.Default.Restore(request.ArchivePath, request.Components);
                DiagnosticLog.Write($"Backup restored. Safety backup: {safetyBackup}");
            }
            catch (Exception exception)
            {
                restoreError = exception;
                DiagnosticLog.Write($"Backup restore failed: {exception}");
            }
        }

        DiagnosticLog.Flush();
        base.OnExit(e);

        if (_pendingRestore is not null && Environment.ProcessPath is { } executablePath)
        {
            if (restoreError is not null)
            {
                MessageBox.Show($"恢复失败：{restoreError.Message}", "TileStart", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
        }
    }
}