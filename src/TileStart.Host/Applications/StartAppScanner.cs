using System.IO;
using System.Runtime.InteropServices;
using TileStart.Host.Shell;
using TileStart.Host.Utilities;

namespace TileStart.Host.Applications;

public static class StartAppScanner
{
    private static readonly string[] ShortcutExtensions = [".lnk", ".url", ".appref-ms"];
    // Win10 AppResolver 用该 Shell 属性区分真正的 AppsFolder launcher；只看 PFN 会把目录模型降级成近似实现。
    private const string MetroAppLauncherProperty = "{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3} 14";
    private const string DesktopAppUserModelId = "Microsoft.Windows.Desktop";

    public static Task<IReadOnlyList<AppEntry>> ScanAsync()
    {
        return RunOnBackgroundThread(
            ScanShellCatalog,
            "TileStart Shell Application Scanner",
            ApartmentState.STA);
    }

    private static IReadOnlyList<AppEntry> ScanShellCatalog()
    {
        object? shell = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application") ??
                            throw new InvalidOperationException("Shell.Application is unavailable.");
            shell = Activator.CreateInstance(shellType);
            using var identityResolver = LaunchTargetIdentity.CreateResolver();
            var shortcuts = ScanStartMenuNamespaces(shell!, identityResolver);
            var shortcutEntries = StartMenuFolderBuilder.Build(shortcuts);
            var representedIdentities = AppEntry.FlattenApplications(shortcutEntries)
                .Select(entry => entry.CatalogIdentity)
                .Where(identity => !string.IsNullOrWhiteSpace(identity))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var packagedApps = ScanAppsFolder(shell!, representedIdentities);
            var applications = shortcutEntries
                .Where(entry => !entry.IsFolder)
                .Concat(packagedApps)
                .GroupBy(GetCatalogIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(app => app.AddedAt).First());
            var apps = shortcutEntries
                .Where(entry => entry.IsFolder)
                .Concat(applications)
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            DiagnosticLog.Write(
                $"Application scan completed: {apps.Length} top-level entries, {shortcuts.Count} Start menu items, {packagedApps.Count} packaged launchers.");
            return apps;
        }
        finally
        {
            ReleaseComObject(shell);
        }
    }

    private static IReadOnlyList<StartMenuShortcut> ScanStartMenuNamespaces(
        object shell,
        LaunchTargetIdentity.Resolver identityResolver)
    {
        var directories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        };
        var excludedDirectories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            TaskbarPinner.ShortcutRoot,
        }.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        var shortcuts = new List<StartMenuShortcut>();
        foreach (var directory in directories.Where(Directory.Exists))
        {
            object? folder = null;
            try
            {
                folder = ((dynamic)shell).NameSpace(directory);
                if (folder is not null)
                {
                    ScanShellFolder(folder, [], shortcuts, excludedDirectories, identityResolver,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                                  or COMException)
            {
                DiagnosticLog.Write($"Start menu Shell scan failed for '{directory}': {exception.Message}");
            }
            finally
            {
                ReleaseComObject(folder);
            }
        }

        return shortcuts;
    }

    private static void ScanShellFolder(
        object folder,
        IReadOnlyList<string> displayPath,
        ICollection<StartMenuShortcut> shortcuts,
        IReadOnlyList<string> excludedDirectories,
        LaunchTargetIdentity.Resolver identityResolver,
        ISet<string> visitedFolders)
    {
        object? items = null;
        try
        {
            items = ((dynamic)folder).Items();
            dynamic itemCollection = items!;
            var count = (int)itemCollection.Count;
            for (var index = 0; index < count; index++)
            {
                object? item = null;
                object? childFolder = null;
                try
                {
                    item = itemCollection.Item(index);
                    dynamic shellItem = item!;
                    var path = (string?)shellItem.Path;
                    var name = (string?)shellItem.Name;
                    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name)
                                                        || IsExcludedStartMenuShortcut(path, excludedDirectories)
                                                        || IsHiddenPath(path))
                    {
                        continue;
                    }

                    if ((bool)shellItem.IsFolder)
                    {
                        var normalizedPath = Path.GetFullPath(path);
                        if (!visitedFolders.Add(normalizedPath))
                        {
                            continue;
                        }

                        childFolder = shellItem.GetFolder;
                        if (childFolder is not null)
                        {
                            // 必须沿用 Shell 显示名；物理目录名会把 Accessibility、Administrative Tools
                            // 等资源化目录错误地暴露为英文，并与原生 All Apps 分组不一致。
                            ScanShellFolder(childFolder, [.. displayPath, name], shortcuts, excludedDirectories,
                                identityResolver, visitedFolders);
                        }

                        continue;
                    }

                    if (!ShortcutExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var appUserModelId = GetShellStringProperty(shellItem, "System.AppUserModel.ID");
                    var catalogIdentity = GetCatalogIdentity(path, appUserModelId, identityResolver);
                    shortcuts.Add(new StartMenuShortcut(
                        name,
                        path,
                        File.GetCreationTime(path),
                        string.Join(Path.DirectorySeparatorChar, displayPath),
                        catalogIdentity,
                        appUserModelId));
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Write($"Start menu Shell item scan failed: {exception.Message}");
                }
                finally
                {
                    ReleaseComObject(childFolder);
                    ReleaseComObject(item);
                }
            }
        }
        finally
        {
            ReleaseComObject(items);
        }
    }

    private static IReadOnlyList<AppEntry> ScanAppsFolder(
        object shell,
        IReadOnlySet<string> representedIdentities)
    {
        var apps = new List<AppEntry>();
        object? folder = null;
        object? items = null;
        try
        {
            folder = ((dynamic)shell).NameSpace("shell:AppsFolder");
            if (folder is null)
            {
                return apps;
            }

            items = ((dynamic)folder).Items();
            dynamic itemCollection = items!;
            var count = (int)itemCollection.Count;
            for (var index = 0; index < count; index++)
            {
                object? item = null;
                try
                {
                    item = itemCollection.Item(index);
                    dynamic app = item!;
                    var name = (string?)app.Name;
                    var parsingPath = (string?)app.Path;
                    var appUserModelId = GetShellStringProperty(app, "System.AppUserModel.ID");
                    var parentAppUserModelId = GetShellStringProperty(app, "System.AppUserModel.ParentID");
                    var launcherKind = GetShellIntProperty(app, MetroAppLauncherProperty);
                    if (!IsAppsFolderLauncher(name, appUserModelId, parentAppUserModelId, launcherKind))
                    {
                        continue;
                    }

                    var catalogIdentity = $"AUMID:{appUserModelId}";
                    if (representedIdentities.Contains(catalogIdentity))
                    {
                        continue;
                    }

                    var packageInstallPath =
                        GetShellStringProperty(app, "System.AppUserModel.PackageInstallPath");
                    var launchTarget = $"shell:AppsFolder\\{parsingPath ?? appUserModelId}";
                    apps.Add(AppEntry.Application(name!, launchTarget, DateTime.MinValue, null,
                        packageInstallPath, appUserModelId, catalogIdentity: catalogIdentity));
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Write($"AppsFolder item scan failed: {exception.Message}");
                }
                finally
                {
                    ReleaseComObject(item);
                }
            }

            return apps;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"AppsFolder scan failed: {exception}");
            return apps;
        }
        finally
        {
            ReleaseComObject(items);
            ReleaseComObject(folder);
        }
    }

    private static Task<T> RunOnBackgroundThread<T>(
        Func<T> action,
        string name,
        ApartmentState apartmentState)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = name,
            // 应用枚举属于维护工作，不能与开始菜单动画争抢普通优先级 CPU 时间。
            Priority = ThreadPriority.Lowest,
        };
        thread.SetApartmentState(apartmentState);
        thread.Start();
        return completion.Task;
    }

    internal static bool IsAppsFolderLauncher(
        string? name,
        string? appUserModelId,
        string? parentAppUserModelId,
        int launcherKind) =>
        !string.IsNullOrWhiteSpace(name)
        && !string.IsNullOrWhiteSpace(appUserModelId)
        && string.IsNullOrWhiteSpace(parentAppUserModelId)
        && launcherKind != 0
        && !appUserModelId.Equals(DesktopAppUserModelId, StringComparison.OrdinalIgnoreCase);

    internal static bool IsExcludedStartMenuShortcut(
        string path,
        IEnumerable<string> excludedDirectories) =>
        excludedDirectories.Any(directory => IsPathWithinDirectory(path, directory));

    internal static bool IsPathWithinDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var candidate = Path.GetFullPath(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCatalogIdentity(AppEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.CatalogIdentity)
            ? entry.CatalogIdentity
            : $"TARGET:{LaunchTargetIdentity.GetKey(entry.LaunchTarget)}";

    private static string GetCatalogIdentity(
        string launchTarget,
        string appUserModelId,
        LaunchTargetIdentity.Resolver identityResolver) =>
        !string.IsNullOrWhiteSpace(appUserModelId)
            ? $"AUMID:{appUserModelId}"
            : $"TARGET:{identityResolver.GetKey(launchTarget)}";

    private static bool IsHiddenPath(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Hidden);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string GetShellStringProperty(dynamic item, string propertyName)
    {
        try
        {
            return item.ExtendedProperty(propertyName) as string ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static int GetShellIntProperty(dynamic item, string propertyName)
    {
        try
        {
            var value = item.ExtendedProperty(propertyName);
            return value is null ? 0 : Convert.ToInt32(value);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}