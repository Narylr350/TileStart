using System.IO;
using System.Windows.Media;
using TileStart.Host.Icons;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Utilities;

public static class DroppedTileFactory
{
    private static readonly string[] ApplicationExtensions = [".exe", ".lnk", ".appref-ms"];
    private static readonly string[] ScriptExtensions = [".bat", ".cmd", ".ps1"];

    public static TileItem? Create(string path) => Create(path, ShellIconLoader.Load);

    internal static TileItem? Create(string path, Func<string, ImageSource?> iconLoader)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var isDirectory = Directory.Exists(fullPath);
            if (!isDirectory && !File.Exists(fullPath))
            {
                return null;
            }

            var type = Classify(fullPath, isDirectory);
            var name = isDirectory
                ? new DirectoryInfo(fullPath).Name
                : type == TileTargetType.File
                    ? Path.GetFileName(fullPath)
                    : Path.GetFileNameWithoutExtension(fullPath);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = fullPath;
            }

            return new TileItem
            {
                Name = name,
                LaunchTarget = fullPath,
                TargetType = type,
                Size = TileSize.Medium,
                Icon = iconLoader(fullPath),
            };
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException
                                              or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Unable to pin dropped target '{path}': {exception.Message}");
            return null;
        }
    }

    private static TileTargetType Classify(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            return TileTargetType.Folder;
        }

        var extension = Path.GetExtension(path);
        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            return TileTargetType.Url;
        }

        if (ScriptExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return TileTargetType.Script;
        }

        return ApplicationExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            ? TileTargetType.Application
            : TileTargetType.File;
    }
}