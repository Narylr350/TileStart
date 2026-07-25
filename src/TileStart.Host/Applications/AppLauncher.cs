using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Utilities;

namespace TileStart.Host.Applications;

public static class AppLauncher
{
    private static readonly string[] DirectLaunchExtensions =
        [".exe", ".lnk", ".appref-ms", ".bat", ".cmd", ".url"];

    public static bool Launch(AppEntry app) =>
        Launch(app.Name, CreateShellTargetStartInfo(app.LaunchTarget));

    public static bool Launch(TileItem tile) => Launch(tile.Name, CreateStartInfo(tile));

    public static bool LaunchShellTarget(string name, string target) =>
        Launch(name, new ProcessStartInfo(target) { UseShellExecute = true });

    public static bool LaunchProcess(string name, string fileName, string arguments) =>
        Launch(name, new ProcessStartInfo(fileName)
        {
            Arguments = arguments,
            UseShellExecute = true,
        });

    public static bool LaunchAsAdministrator(TileItem tile)
    {
        return Launch(tile.Name, CreateStartInfo(tile, true));
    }

    public static bool OpenFileLocation(AppEntry app)
    {
        return app.CanOpenFileLocation && File.Exists(app.LaunchTarget)
                                       && Launch(app.Name, CreateOpenFileLocationStartInfo(app.LaunchTarget));
    }

    public static bool CanOpenFileLocation(TileItem tile, IReadOnlyList<AppEntry> apps) =>
        ResolveOpenFileLocationTarget(tile, apps) is not null;

    public static bool OpenFileLocation(TileItem tile, IReadOnlyList<AppEntry> apps)
    {
        var target = ResolveOpenFileLocationTarget(tile, apps);
        return target is not null && Launch(tile.Name, CreateOpenFileLocationStartInfo(target));
    }

    internal static string? ResolveOpenFileLocationTarget(TileItem tile, IReadOnlyList<AppEntry> apps)
    {
        if (tile.IsTileFolder)
        {
            return null;
        }

        const string appsFolderPrefix = @"shell:AppsFolder\";
        var isAppsFolderTarget = tile.LaunchTarget.StartsWith(appsFolderPrefix, StringComparison.OrdinalIgnoreCase);
        var localTarget = isAppsFolderTarget ? tile.LaunchTarget[appsFolderPrefix.Length..] : tile.LaunchTarget;
        if (!isAppsFolderTarget && (File.Exists(localTarget) || Directory.Exists(localTarget)))
        {
            return localTarget;
        }

        if (tile.TargetType == TileTargetType.Application)
        {
            var shortcut = apps.FirstOrDefault(app =>
                app.CanOpenFileLocation
                && File.Exists(app.LaunchTarget)
                && app.Name.Equals(tile.Name, StringComparison.CurrentCultureIgnoreCase));
            if (shortcut is not null)
            {
                return shortcut.LaunchTarget;
            }
        }

        return File.Exists(localTarget) || Directory.Exists(localTarget) ? localTarget : null;
    }

    internal static ProcessStartInfo CreateOpenFileLocationStartInfo(string shortcutPath) =>
        new("explorer.exe")
        {
            Arguments = $"/select,\"{shortcutPath}\"",
            UseShellExecute = true,
        };

    internal static ProcessStartInfo CreateStartInfo(TileItem tile, bool forceAdministrator = false)
    {
        var isPowerShellScript =
            Path.GetExtension(tile.LaunchTarget).Equals(".ps1", StringComparison.OrdinalIgnoreCase);
        var startInfo = isPowerShellScript
            ? new ProcessStartInfo("powershell.exe")
            {
                Arguments =
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{tile.LaunchTarget}\"{AppendArguments(tile.Arguments)}",
                UseShellExecute = true,
            }
            : CreateShellTargetStartInfo(tile.LaunchTarget, tile.Arguments);

        if (!string.IsNullOrWhiteSpace(tile.WorkingDirectory))
        {
            startInfo.WorkingDirectory = tile.WorkingDirectory;
        }

        if (tile.RunAsAdministrator || forceAdministrator)
        {
            startInfo.Verb = "runas";
        }

        return startInfo;
    }

    internal static ProcessStartInfo CreateShellTargetStartInfo(
        string target,
        string arguments = "",
        bool? hasFileAssociation = null)
    {
        if (Directory.Exists(target))
        {
            return new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"\"{target}\"",
                UseShellExecute = true,
            };
        }

        if (File.Exists(target) && ShouldOpenFileLocation(target, hasFileAssociation))
        {
            return CreateOpenFileLocationStartInfo(target);
        }

        return new ProcessStartInfo(target)
        {
            Arguments = arguments,
            UseShellExecute = true,
        };
    }

    private static bool ShouldOpenFileLocation(string path, bool? hasFileAssociation)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return true;
        }

        if (DirectLaunchExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return !(hasFileAssociation ?? HasDefaultFileAssociation(path));
    }

    private static bool HasDefaultFileAssociation(string path)
    {
        var executable = new StringBuilder(1024);
        return FindExecutableW(path, null, executable).ToInt64() > 32;
    }

    private static string AppendArguments(string arguments)
    {
        return string.IsNullOrWhiteSpace(arguments) ? string.Empty : $" {arguments}";
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindExecutableW(string file, string? directory, StringBuilder result);

    private static bool Launch(string name, ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Unable to launch '{name}' from '{startInfo.FileName}': {exception}");
            return false;
        }
    }
}