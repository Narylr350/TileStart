using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;
using TileStart.Host.Utilities;

namespace TileStart.Host.Shell;

public static class ExplorerContextMenuRegistration
{
    internal static readonly string[] RegistrationClasses = ["*", "Directory"];
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
                RegisterCommand(registrationClass, "TileStart.AddToAppList", "添加到 TileStart 应用列表",
                    executablePath, "--add-app-list");
                RegisterCommand(registrationClass, "TileStart.PinTile", "添加到 TileStart 磁贴区", executablePath,
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
        command.SetValue(null, $"\"{executablePath}\" {argument} \"%1\"");
    }

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