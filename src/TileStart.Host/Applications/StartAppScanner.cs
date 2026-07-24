using System.IO;
using System.Runtime.InteropServices;
using TileStart.Host.Shell;
using TileStart.Host.Utilities;

namespace TileStart.Host.Applications;

public static class StartAppScanner
{
    private static readonly string[] ShortcutExtensions = [".lnk", ".url", ".appref-ms"];

    public static async Task<IReadOnlyList<AppEntry>> ScanAsync()
    {
        var shortcutTask = Task.Run(ScanShortcuts);
        var packagedTask = ScanPackagedAppsAsync();
        await Task.WhenAll(shortcutTask, packagedTask);

        var shortcutEntries = shortcutTask.Result;
        var applications = shortcutEntries
            .Where(entry => !entry.IsFolder)
            .Concat(packagedTask.Result)
            .GroupBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => group.OrderByDescending(app => app.AddedAt).First());
        var apps = shortcutEntries
            .Where(entry => entry.IsFolder)
            .Concat(applications)
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        DiagnosticLog.Write($"Application scan completed: {apps.Length} entries.");
        return apps;
    }

    private static IReadOnlyList<AppEntry> ScanShortcuts()
    {
        var directories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
        };
        var shortcuts = new List<StartMenuShortcut>();
        foreach (var directory in directories.Where(Directory.Exists))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (path.StartsWith(TaskbarPinner.ShortcutRoot + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!ShortcutExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = Path.GetFileNameWithoutExtension(path);
                    var parent = Path.GetDirectoryName(path) ?? directory;
                    var relativeFolder = Path.GetRelativePath(directory, parent);
                    shortcuts.Add(new StartMenuShortcut(name,
                        path,
                        File.GetCreationTime(path),
                        relativeFolder == "." ? string.Empty : relativeFolder));
                }
            }
            catch (IOException exception)
            {
                DiagnosticLog.Write($"Start menu scan failed for '{directory}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                DiagnosticLog.Write($"Start menu scan denied for '{directory}': {exception.Message}");
            }
        }

        return StartMenuFolderBuilder.Build(shortcuts);
    }

    private static Task<IReadOnlyList<AppEntry>> ScanPackagedAppsAsync()
    {
        var completion =
            new TaskCompletionSource<IReadOnlyList<AppEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var apps = new List<AppEntry>();
            object? shell = null;
            object? folder = null;
            object? items = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application") ??
                                throw new InvalidOperationException("Shell.Application is unavailable.");
                shell = Activator.CreateInstance(shellType);
                dynamic shellApi = shell!;
                folder = shellApi.NameSpace("shell:AppsFolder");
                if (folder is null)
                {
                    completion.SetResult(apps);
                    return;
                }

                dynamic folderApi = folder;
                items = folderApi.Items();
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
                        var appUserModelId = (string?)app.Path;
                        var packageInstallPath =
                            (string?)app.ExtendedProperty("System.AppUserModel.PackageInstallPath");
                        var packageFamilyName = (string?)app.ExtendedProperty("System.AppUserModel.PackageFamilyName");
                        if (!string.IsNullOrWhiteSpace(name) &&
                            !string.IsNullOrWhiteSpace(appUserModelId) &&
                            IsPackagedAppsFolderItem(packageFamilyName))
                        {
                            var launchTarget = $"shell:AppsFolder\\{appUserModelId}";
                            apps.Add(AppEntry.Application(name, launchTarget, DateTime.MinValue, null,
                                packageInstallPath ?? string.Empty, appUserModelId));
                        }
                    }
                    finally
                    {
                        ReleaseComObject(item);
                    }
                }

                completion.SetResult(apps);
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Packaged application scan failed: {exception}");
                completion.SetResult(apps);
            }
            finally
            {
                ReleaseComObject(items);
                ReleaseComObject(folder);
                ReleaseComObject(shell);
            }
        })
        {
            IsBackground = true,
            Name = "TileStart AppsFolder Scanner",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    internal static bool IsPackagedAppsFolderItem(string? packageFamilyName) =>
        !string.IsNullOrWhiteSpace(packageFamilyName);

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}