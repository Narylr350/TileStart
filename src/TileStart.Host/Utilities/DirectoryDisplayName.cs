using System.IO;

namespace TileStart.Host.Utilities;

internal static class DirectoryDisplayName
{
    public static string Get(string path)
    {
        var root = Path.GetPathRoot(path);
        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new DirectoryInfo(path).Name;
        }

        // DirectoryInfo.Name returns the raw path (for example C:\) for a drive
        // root. Match Explorer's useful identity instead of exposing that syntax.
        root ??= path;
        var driveDesignator = normalizedRoot ?? normalizedPath;
        try
        {
            var volumeLabel = new DriveInfo(root).VolumeLabel;
            return string.IsNullOrWhiteSpace(volumeLabel)
                ? driveDesignator
                : $"{volumeLabel} ({driveDesignator})";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return driveDesignator;
        }
    }
}
