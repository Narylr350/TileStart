using System.IO;
using System.Runtime.InteropServices;
using TileStart.Host.Shell;

namespace TileStart.Host.Applications;

internal static class LaunchTargetIdentity
{
    public static string GetKey(string launchTarget)
    {
        var normalized = TaskbarPinner.NormalizeDisplayName(launchTarget);
        var shortcutTarget = ResolveShortcutTarget(normalized);
        if (!string.IsNullOrWhiteSpace(shortcutTarget))
        {
            normalized = shortcutTarget;
        }

        try
        {
            if (File.Exists(normalized) || Directory.Exists(normalized))
            {
                normalized = Path.GetFullPath(normalized);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return normalized.Trim().ToUpperInvariant();
    }

    private static string? ResolveShortcutTarget(string path)
    {
        if (!File.Exists(path) || !Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            shell = shellType is null ? null : Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            shortcut = ((dynamic)shell).CreateShortcut(path);
            var targetPath = ((dynamic)shortcut).TargetPath as string;
            return !string.IsNullOrWhiteSpace(targetPath) ? targetPath : null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
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
