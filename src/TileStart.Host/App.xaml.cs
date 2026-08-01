using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using TileStart.Host.About;
using TileStart.Host.Backup;
using TileStart.Host.Compatibility;
using TileStart.Host.Settings;
using TileStart.Host.Shell;
using TileStart.Host.Themes;
using TileStart.Host.Updates;
using TileStart.Host.Utilities;
using TileStart.Host.Windowing;

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
    private bool _isSystemThemeSubscribed;
    private bool _isRestartScheduled;
    private bool _resolvedDarkMode;
    private AppearancePreferences _appearancePreferences = new();

    public App()
    {
        DispatcherUnhandledException += (_, args) => DiagnosticLog.Write($"Dispatcher exception: {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException +=
            (_, args) => DiagnosticLog.Write($"Unhandled exception: {args.ExceptionObject}");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (TryHandleCompatibilityCommand(e.Args))
        {
            return;
        }

        if (!WaitForPreviousProcess(e.Args))
        {
            Shutdown(1);
            return;
        }

        DiagnosticLog.Write("Host startup started.");

        var startupRequest = HostRequest.FromArguments(e.Args);
        var shutdownRequested = startupRequest.Kind == HostRequestKind.Exit;
        _singleInstance = new SingleInstanceGuard();
        if (!_singleInstance.IsPrimaryInstance)
        {
            if (!SingleInstanceGuard.NotifyPrimaryInstance(startupRequest))
            {
                DiagnosticLog.Write($"Unable to notify primary Host instance: request={startupRequest.Kind}.");
            }

            Shutdown();
            return;
        }

        if (shutdownRequested)
        {
            Shutdown();
            return;
        }

        _appearancePreferences = AppearancePreferencesStore.Load();
        _resolvedDarkMode = AppThemeManager.ResolveDarkMode(_appearancePreferences.ColorMode);
        AppThemeManager.Apply(Resources, _appearancePreferences.ThemeStyle, _appearancePreferences.ColorMode);
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        _isSystemThemeSubscribed = true;

        DiagnosticLog.Write("Creating main window.");
        MainWindow = new MainWindow(_appearancePreferences.ThemeStyle);
        PrimeApplicationActivation();
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
            _appearancePreferences.ThemeStyle,
            _resolvedDarkMode,
            OpenSettings,
            ExitApplication);
        if (e.Args.Length > 0 && startupRequest.Kind is not HostRequestKind.Exit and not HostRequestKind.Open)
        {
            Dispatcher.BeginInvoke(() => HandleHostRequest(startupRequest));
        }

        DiagnosticLog.Write("Host startup completed.");
        ThreadPool.UnsafeQueueUserWorkItem(static _ => StartupRegistration.MigrateLegacyRegistration(), null);
    }

    private bool TryHandleCompatibilityCommand(IReadOnlyList<string> arguments)
    {
        if (arguments.Contains("--remove-startup-registration", StringComparer.OrdinalIgnoreCase))
        {
            var startupRemoved = StartupRegistration.SetEnabled(false);
            DiagnosticLog.Write($"Login startup removal: success={startupRemoved}.");
            DiagnosticLog.Flush();
            Shutdown(startupRemoved ? 0 : 1);
            return true;
        }

        var remove = arguments.Contains("--remove-nvidia-overlay-configuration",
            StringComparer.OrdinalIgnoreCase);
        if (!remove && !arguments.Contains("--configure-nvidia-overlay", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var success = remove
            ? NvidiaOverlayCompatibility.TryRemove(out var detail)
            : NvidiaOverlayCompatibility.TryApply(out detail);
        DiagnosticLog.Write($"NVIDIA Overlay compatibility command: success={success}, remove={remove}, {detail}");
        DiagnosticLog.Flush();
        Shutdown(success ? 0 : 1);
        return true;
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

    private static void PrimeApplicationActivation()
    {
        var activationWindow = new Window
        {
            Width = 1,
            Height = 1,
            Left = -32000,
            Top = -32000,
            Opacity = 0,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowStyle = WindowStyle.None,
        };
        activationWindow.Show();
        activationWindow.Hide();
        activationWindow.Close();
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

    private void OpenBackupAndRestore(Window owner)
    {
        var dialog = new BackupRestoreWindow(ScheduleRestore) { Owner = owner };
        dialog.ShowDialog();
    }

    private void OpenSettings()
    {
        Dispatcher.BeginInvoke(() =>
        {
            var dialog = new SettingsWindow(
                _appearancePreferences.ThemeStyle,
                _appearancePreferences.ColorMode,
                OpenBackupAndRestore,
                OpenAbout);
            if (MainWindow?.IsVisible == true)
            {
                dialog.Owner = MainWindow;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (dialog.ShowDialog() == true)
            {
                ChangeAppearance(dialog.SelectedThemeStyle, dialog.SelectedColorMode);
            }
        });
    }

    private void OpenAbout(Window owner)
    {
        var dialog = new AboutWindow(CheckForUpdatesAsync) { Owner = owner };
        dialog.ShowDialog();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isCheckingForUpdates)
        {
            TileStartMessageDialog.Show(null, "检查更新", "正在检查更新，请稍候。");
            return;
        }

        _isCheckingForUpdates = true;
        UpdateProgressWindow? progressWindow = null;
        try
        {
            using var checkTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var release = await _updateService.GetLatestReleaseAsync(checkTimeout.Token);
            var currentVersion = GitHubUpdateService.CurrentVersion;
            if (!GitHubUpdateService.IsNewer(currentVersion, release.Version))
            {
                TileStartMessageDialog.Show(
                    null,
                    "检查更新",
                    $"当前版本 {currentVersion.ToString(3)} 已是最新版本。");
                return;
            }

            var installedCopy = GitHubUpdateService.IsInstalledCopy(Environment.ProcessPath);
            var packageDescription = installedCopy ? "安装器" : "便携版压缩包";
            var shouldDownload = TileStartMessageDialog.Confirm(
                null,
                "发现新版本",
                $"发现新版本 {release.Version.ToString(3)}（当前 {currentVersion.ToString(3)}）。\n\n是否从 GitHub 下载并校验{packageDescription}？",
                TileStartMessageKind.Question,
                primaryText: "下载并校验",
                secondaryText: "暂不更新");
            if (!shouldDownload)
            {
                return;
            }

            using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            progressWindow = new UpdateProgressWindow();
            var update = progressWindow.Run(
                (progress, cancellationToken) =>
                    _updateService.DownloadAsync(release, installedCopy, progress, cancellationToken),
                downloadTimeout.Token);
            if (update.Kind == UpdatePackageKind.Installer)
            {
                // 助手先等待当前 Host 完全退出，安装器才会覆盖文件；否则 Injector/Hook 清理与安装会发生竞态。
                UpdateInstallerLauncher.LaunchAfterHostExit(update.Path, Environment.ProcessId);
                ((MainWindow)MainWindow).AllowClose();
                Shutdown();
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{update.Path}\"")
            {
                UseShellExecute = true,
            });
            TileStartMessageDialog.Show(
                null,
                "更新包已就绪",
                "便携版已下载并通过校验。请退出 TileStart 后解压覆盖旧文件。");
        }
        catch (OperationCanceledException) when (progressWindow?.UserCanceled == true)
        {
            // 用户主动取消不再弹出“超时”，进度窗口本身已经给出即时反馈。
        }
        catch (OperationCanceledException)
        {
            TileStartMessageDialog.Show(
                null,
                "更新超时",
                "检查或下载更新超时，请稍后重试。",
                TileStartMessageKind.Warning);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Update check failed: {exception}");
            TileStartMessageDialog.Show(
                null,
                "无法完成更新",
                exception.Message,
                TileStartMessageKind.Error);
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

    private void ChangeAppearance(AppThemeStyle themeStyle, AppColorMode colorMode)
    {
        if (_appearancePreferences.ThemeStyle == themeStyle && _appearancePreferences.ColorMode == colorMode)
        {
            return;
        }

        _appearancePreferences.ThemeStyle = themeStyle;
        _appearancePreferences.ColorMode = colorMode;
        AppearancePreferencesStore.Save(_appearancePreferences);
        ScheduleApplicationRestart();
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var resolvedDarkMode = AppThemeManager.ResolveDarkMode(_appearancePreferences.ColorMode);
            if (!ShouldRestartForUserPreferenceChange(
                    e.Category,
                    _appearancePreferences.ColorMode,
                    _resolvedDarkMode,
                    resolvedDarkMode))
            {
                return;
            }

            _resolvedDarkMode = resolvedDarkMode;
            ScheduleApplicationRestart();
        });
    }

    internal static bool ShouldRestartForUserPreferenceChange(
        UserPreferenceCategory category,
        AppColorMode colorMode,
        bool previousDarkMode,
        bool resolvedDarkMode) =>
        // Accent resources remain active in explicit Light/Dark modes, so a Color change must
        // reload the process independently from the system light/dark-mode decision.
        category == UserPreferenceCategory.Color
        || (colorMode == AppColorMode.System && previousDarkMode != resolvedDarkMode);

    private void ScheduleApplicationRestart()
    {
        if (_isRestartScheduled)
        {
            return;
        }

        _isRestartScheduled = true;
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.PrepareForApplicationRestart();
        }

        Dispatcher.BeginInvoke(RestartApplication, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void RestartApplication()
    {
        if (Environment.ProcessPath is { } executablePath)
        {
            Process.Start(new ProcessStartInfo(
                executablePath,
                $"--wait-for-process {Environment.ProcessId}")
            {
                UseShellExecute = true,
            });
        }

        ((MainWindow)MainWindow).AllowClose();
        Shutdown();
    }

    private static bool WaitForPreviousProcess(IReadOnlyList<string> arguments)
    {
        var processId = ReadWaitProcessId(arguments);
        if (processId is null || processId == Environment.ProcessId)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return process.WaitForExit(TimeSpan.FromSeconds(15));
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    internal static int? ReadWaitProcessId(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index].Equals("--wait-for-process", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(arguments[index + 1], out var processId)
                && processId > 0)
            {
                return processId;
            }
        }

        return null;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_isSystemThemeSubscribed)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        }

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

        if (_pendingRestore is not null && restoreError is not null)
        {
            TileStartMessageDialog.Show(
                null,
                "恢复失败",
                restoreError.Message,
                TileStartMessageKind.Error);
        }

        DiagnosticLog.Flush();
        base.OnExit(e);

        if (_pendingRestore is not null && Environment.ProcessPath is { } executablePath)
        {
            Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
        }
    }
}
