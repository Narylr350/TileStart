using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Icons;

internal static partial class PackagedTileAssetLoader
{
    public static ImageSource? Load(string packageInstallPath, string appUserModelId, TileSize size)
    {
        var path = ResolveAssetPath(packageInstallPath, appUserModelId, size);
        if (path is null)
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    internal static string? ResolveAssetPath(string packageInstallPath, string appUserModelId, TileSize size)
    {
        if (string.IsNullOrWhiteSpace(packageInstallPath) || string.IsNullOrWhiteSpace(appUserModelId))
        {
            return null;
        }

        var manifestPath = Path.Combine(packageInstallPath, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var applicationId = appUserModelId[(appUserModelId.LastIndexOf('!') + 1)..];
            var document = XDocument.Load(manifestPath);
            var application = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Application"
                    && element.Attributes().Any(attribute => attribute.Name.LocalName == "Id"
                        && attribute.Value.Equals(applicationId, StringComparison.OrdinalIgnoreCase)));
            var visualElements = application?.Elements().FirstOrDefault(element => element.Name.LocalName == "VisualElements");
            var defaultTile = visualElements?.Elements().FirstOrDefault(element => element.Name.LocalName == "DefaultTile");
            var asset = size switch
            {
                TileSize.Small => Attribute(defaultTile, "Square71x71Logo"),
                TileSize.Wide => Attribute(defaultTile, "Wide310x150Logo"),
                TileSize.Large => Attribute(defaultTile, "Square310x310Logo"),
                _ => null,
            } ?? Attribute(visualElements, "Square150x150Logo");

            return asset is null ? null : ResolveQualifiedAsset(packageInstallPath, asset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? Attribute(XElement? element, string name) =>
        element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;

    private static string? ResolveQualifiedAsset(string packageInstallPath, string relativePath)
    {
        var root = Path.GetFullPath(packageInstallPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var unqualifiedPath = Path.GetFullPath(Path.Combine(packageInstallPath, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
        if (!unqualifiedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (File.Exists(unqualifiedPath))
        {
            return unqualifiedPath;
        }

        var directory = Path.GetDirectoryName(unqualifiedPath);
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }

        var stem = Path.GetFileNameWithoutExtension(unqualifiedPath);
        return Directory.EnumerateFiles(directory, $"{stem}*.png")
            .Where(path => !Path.GetFileName(path).Contains("_contrast-", StringComparison.OrdinalIgnoreCase))
            .Select(path => new { Path = path, Scale = Scale(path) })
            .OrderBy(candidate => Math.Abs(candidate.Scale - 150))
            .ThenByDescending(candidate => candidate.Scale)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static int Scale(string path)
    {
        var match = ScalePattern().Match(Path.GetFileName(path));
        return match.Success && int.TryParse(match.Groups[1].Value, out var scale) ? scale : 100;
    }

    [GeneratedRegex(@"\.scale-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ScalePattern();
}
