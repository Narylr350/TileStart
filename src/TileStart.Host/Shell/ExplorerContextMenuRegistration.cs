using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;
using TileStart.Host.Utilities;

namespace TileStart.Host.Shell;

public static class ExplorerContextMenuRegistration
{
    internal static readonly string[] RegistrationClasses = ["*", "Directory", "Drive"];
    internal const string AddToAppListLabel = "添加到 TileStart 应用列表";
    internal const string PinToStartLabel = "固定到“开始”屏幕";
    private static readonly string[] LegacyExtensions = [".exe", ".lnk", ".appref-ms"];

    public static void EnsureRegistered()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        try
        {
            RemoveLegacyRegistrations();
            foreach (var registrationClass in RegistrationClasses)
            {
                RegisterCommand(registrationClass, "TileStart.AddToAppList", AddToAppListLabel,
                    executablePath, "--add-app-list");
                RegisterCommand(registrationClass, "TileStart.PinTile", PinToStartLabel, executablePath,
                    "--pin-tile");
            }

            SHChangeNotify(0x08000000, 0, 0, 0);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException
                                              or System.Security.SecurityException)
        {
            DiagnosticLog.Write($"Explorer context menu registration failed: {exception.Message}");
        }
    }

    private static void RegisterCommand(
        string registrationClass,
        string commandKey,
        string label,
        string executablePath,
        string argument)
    {
        var keyPath = $@"Software\Classes\{registrationClass}\shell\{commandKey}";
        using var menuKey = Registry.CurrentUser.CreateSubKey(keyPath);
        menuKey.SetValue(null, label);
        menuKey.SetValue("Icon", executablePath);
        using var command = menuKey.CreateSubKey("command");
        command.SetValue(null, BuildCommand(executablePath, argument, registrationClass));
    }

    internal static string BuildCommand(string executablePath, string argument, string registrationClass) =>
        registrationClass.Equals("Drive", StringComparison.OrdinalIgnoreCase)
            // Quoting a root such as C:\ produces "C:\", whose trailing slash
            // escapes the closing quote in Windows command-line parsing.
            ? $"\"{executablePath}\" {argument} %V"
            : $"\"{executablePath}\" {argument} \"%1\"";

    private static void RemoveLegacyRegistrations()
    {
        foreach (var extension in LegacyExtensions)
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\SystemFileAssociations\{extension}\shell\TileStart.AddToAppList", false);
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\SystemFileAssociations\{extension}\shell\TileStart.PinTile", false);
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, nint item1, nint item2);
}