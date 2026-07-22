using Microsoft.Win32;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Windows.Media;

namespace TileStart.Host;

public static class UserAccountPictureLoader
{
    private static readonly string[] RegistryValueNames = ["Image96", "Image48", "Image40", "Image32", "Image192", "Image240", "Image448"];

    public static ImageSource? Load()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value;
            if (!string.IsNullOrWhiteSpace(sid))
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}");
                foreach (var valueName in RegistryValueNames)
                {
                    if (key?.GetValue(valueName) is string path && ShellIconLoader.LoadImage(path) is { } image)
                    {
                        return image;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            DiagnosticLog.Write($"User account picture lookup failed: {exception.Message}");
        }

        var accountPictures = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Microsoft",
            "User Account Pictures");
        foreach (var fileName in new[] { "user.png", "user-48.png", "user-40.png", "user-32.png" })
        {
            if (ShellIconLoader.LoadImage(Path.Combine(accountPictures, fileName)) is { } image)
            {
                return image;
            }
        }

        return null;
    }
}
