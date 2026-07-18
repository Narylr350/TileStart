using System.Diagnostics;
using System.IO;

namespace TileStart.Host;

public static class AppLauncher
{
    public static bool Launch(AppEntry app) => Launch(app.Name, new ProcessStartInfo(app.LaunchTarget) { UseShellExecute = true });

    public static bool Launch(TileItem tile) => Launch(tile.Name, CreateStartInfo(tile));

    public static bool LaunchAsAdministrator(TileItem tile)
    {
        return Launch(tile.Name, CreateStartInfo(tile, true));
    }

    public static bool OpenFileLocation(AppEntry app)
    {
        return app.CanOpenFileLocation && File.Exists(app.LaunchTarget)
            && Launch(app.Name, CreateOpenFileLocationStartInfo(app.LaunchTarget));
    }

    internal static ProcessStartInfo CreateOpenFileLocationStartInfo(string shortcutPath) =>
        new("explorer.exe")
        {
            Arguments = $"/select,\"{shortcutPath}\"",
            UseShellExecute = true,
        };

    internal static ProcessStartInfo CreateStartInfo(TileItem tile, bool forceAdministrator = false)
    {
        var isPowerShellScript = Path.GetExtension(tile.LaunchTarget).Equals(".ps1", StringComparison.OrdinalIgnoreCase);
        var startInfo = isPowerShellScript
            ? new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tile.LaunchTarget}\"{AppendArguments(tile.Arguments)}",
                UseShellExecute = true,
            }
            : new ProcessStartInfo(tile.LaunchTarget)
            {
                Arguments = tile.Arguments,
                UseShellExecute = true,
            };

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

    private static string AppendArguments(string arguments)
    {
        return string.IsNullOrWhiteSpace(arguments) ? string.Empty : $" {arguments}";
    }

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
