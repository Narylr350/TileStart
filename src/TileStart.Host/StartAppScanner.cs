using System.IO;
using System.Runtime.InteropServices;

namespace TileStart.Host;

public static class StartAppScanner
{
    private static readonly string[] ShortcutExtensions = [".lnk", ".url", ".appref-ms"];

    public static async Task<IReadOnlyList<AppEntry>> ScanAsync()
    {
        var shortcutTask = Task.Run(ScanShortcuts);
        var packagedTask = ScanPackagedAppsAsync();
        await Task.WhenAll(shortcutTask, packagedTask);

        var apps = shortcutTask.Result
            .Concat(packagedTask.Result)
            .GroupBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => group.OrderByDescending(app => app.AddedAt).First())
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
        var apps = new List<AppEntry>();
        foreach (var directory in directories.Where(Directory.Exists))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (!ShortcutExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = Path.GetFileNameWithoutExtension(path);
                    apps.Add(CreateEntry(name, path, File.GetCreationTime(path)));
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

        return apps;
    }

    private static Task<IReadOnlyList<AppEntry>> ScanPackagedAppsAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<AppEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var apps = new List<AppEntry>();
            object? shell = null;
            object? folder = null;
            object? items = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application") ?? throw new InvalidOperationException("Shell.Application is unavailable.");
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
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(appUserModelId))
                        {
                            apps.Add(CreateEntry(name, $"shell:AppsFolder\\{appUserModelId}", DateTime.MinValue));
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

    private static AppEntry CreateEntry(string name, string launchTarget, DateTime addedAt)
    {
        var first = name.Trim().FirstOrDefault();
        var initial = first == default ? "?" : char.ToUpper(first).ToString();
        var sortLetter = first is >= 'A' and <= 'Z' or >= 'a' and <= 'z' ? char.ToUpperInvariant(first).ToString() : "#";
        return new AppEntry
        {
            Name = name,
            LaunchTarget = launchTarget,
            SortLetter = sortLetter,
            Initial = initial,
            AddedAt = addedAt,
            Icon = ShellIconLoader.Load(launchTarget),
        };
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
