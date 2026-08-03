using Microsoft.Win32;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Icons;

internal static partial class PackagedTileAssetLoader
{
    private const string AppxApplicationsRegistryPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications";

    public static ImageSource? Load(string packageInstallPath, string appUserModelId, TileSize size)
    {
        return LoadAsset(ResolveAssetPath(packageInstallPath, appUserModelId, size));
    }

    public static ImageSource? LoadApplicationIcon(string packageInstallPath, string appUserModelId)
    {
        return LoadAsset(ResolveApplicationIconAssetPath(packageInstallPath, appUserModelId));
    }

    public static ImageSource? LoadKnownShellAlias(string launchTarget, TileSize size)
    {
        return LoadAsset(ResolveKnownShellAliasAssetPath(launchTarget, size));
    }

    private static ImageSource? LoadAsset(string? path)
    {
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
            var visualElements = FindVisualElements(manifestPath, appUserModelId);
            var defaultTile = visualElements?.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "DefaultTile");
            var asset = size switch
            {
                TileSize.Small => Attribute(defaultTile, "Square71x71Logo"),
                TileSize.Wide => Attribute(defaultTile, "Wide310x150Logo"),
                TileSize.Large => Attribute(defaultTile, "Square310x310Logo"),
                _ => null,
            } ?? Attribute(visualElements, "Square150x150Logo");

            return asset is null ? null : ResolveQualifiedAsset(packageInstallPath, asset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or System.Xml.XmlException)
        {
            return null;
        }
    }

    internal static string? ResolveApplicationIconAssetPath(string packageInstallPath, string appUserModelId)
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
            var visualElements = FindVisualElements(manifestPath, appUserModelId);
            var asset = Attribute(visualElements, "Square44x44Logo");
            return asset is null ? null : ResolveQualifiedApplicationIcon(packageInstallPath, asset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                               or System.Xml.XmlException)
        {
            return null;
        }
    }

    internal static string? ResolveKnownShellAliasAssetPath(
        string launchTarget,
        TileSize size,
        IEnumerable<string>? packageManifestPaths = null)
    {
        const string appsFolderPrefix = "shell:AppsFolder\\";
        if (!launchTarget.StartsWith(appsFolderPrefix, StringComparison.OrdinalIgnoreCase)
            || !launchTarget[appsFolderPrefix.Length..].Equals("MSEdge", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (var manifestPath in packageManifestPaths ?? EnumerateEdgePackageManifestPaths())
        {
            var packageInstallPath = Path.GetDirectoryName(manifestPath);
            if (packageInstallPath is null)
            {
                continue;
            }

            var path = ResolveFirstApplicationAssetPath(packageInstallPath, size);
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static XElement? FindVisualElements(string manifestPath, string appUserModelId)
    {
        var applicationId = appUserModelId[(appUserModelId.LastIndexOf('!') + 1)..];
        var document = XDocument.Load(manifestPath);
        var application = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Application"
                                       && element.Attributes().Any(attribute => attribute.Name.LocalName == "Id"
                                           && attribute.Value.Equals(applicationId,
                                               StringComparison.OrdinalIgnoreCase)));
        return application?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "VisualElements");
    }

    private static string? ResolveFirstApplicationAssetPath(string packageInstallPath, TileSize size)
    {
        var manifestPath = Path.Combine(packageInstallPath, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(manifestPath);
            var visualElements = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "VisualElements");
            var defaultTile = visualElements?.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "DefaultTile");
            var asset = size switch
            {
                TileSize.Small => Attribute(defaultTile, "Square71x71Logo"),
                TileSize.Wide => Attribute(defaultTile, "Wide310x150Logo"),
                TileSize.Large => Attribute(defaultTile, "Square310x310Logo"),
                _ => null,
            } ?? Attribute(visualElements, "Square150x150Logo");
            return asset is null ? null : ResolveQualifiedAsset(packageInstallPath, asset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                               or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> EnumerateEdgePackageManifestPaths()
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var applications = root.OpenSubKey(AppxApplicationsRegistryPath);
            if (applications is null)
            {
                return [];
            }

            return applications.GetSubKeyNames()
                .Where(name => name.StartsWith("Microsoft.MicrosoftEdge.Stable_",
                                   StringComparison.OrdinalIgnoreCase)
                               || name.StartsWith("Microsoft.MicrosoftEdge_",
                                   StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name =>
                {
                    using var package = applications.OpenSubKey(name);
                    return package?.GetValue("Path") as string;
                })
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Cast<string>()
                .ToArray();
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException
                                               or IOException)
        {
            return [];
        }
    }

    private static string? Attribute(XElement? element, string name) =>
        element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;

    private static string? ResolveQualifiedAsset(string packageInstallPath, string relativePath)
    {
        var unqualifiedPath = ResolveSafePackagePath(packageInstallPath, relativePath);
        if (unqualifiedPath is null)
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

    private static string? ResolveQualifiedApplicationIcon(string packageInstallPath, string relativePath)
    {
        var unqualifiedPath = ResolveSafePackagePath(packageInstallPath, relativePath);
        if (unqualifiedPath is null)
        {
            return null;
        }

        var directory = Path.GetDirectoryName(unqualifiedPath);
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }

        var stem = Path.GetFileNameWithoutExtension(unqualifiedPath);
        var qualified = Directory.EnumerateFiles(directory, $"{stem}*.png")
            .Where(path => !Path.GetFileName(path).Contains("_contrast-", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).Contains("_theme-", StringComparison.OrdinalIgnoreCase))
            .Select(path => new { Path = path, Rank = ApplicationIconRank(path) })
            .OrderBy(candidate => candidate.Rank.Kind)
            .ThenBy(candidate => candidate.Rank.Distance)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();

        return qualified ?? (File.Exists(unqualifiedPath) ? unqualifiedPath : null);
    }

    private static (int Kind, int Distance) ApplicationIconRank(string path)
    {
        var fileName = Path.GetFileName(path);
        var targetSizeMatch = TargetSizePattern().Match(fileName);
        if (targetSizeMatch.Success && int.TryParse(targetSizeMatch.Groups[1].Value, out var targetSize))
        {
            var isUnplated = fileName.Contains("_altform-unplated", StringComparison.OrdinalIgnoreCase);
            var exactKind = targetSize == 24 ? (isUnplated ? 0 : 1) : (isUnplated ? 2 : 3);
            return (exactKind, Math.Abs(targetSize - 24));
        }

        return (4, Math.Abs(Scale(path) - 100));
    }

    private static string? ResolveSafePackagePath(string packageInstallPath, string relativePath)
    {
        var root = Path.GetFullPath(packageInstallPath).TrimEnd(Path.DirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        var path =
            Path.GetFullPath(Path.Combine(packageInstallPath, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private static int Scale(string path)
    {
        var match = ScalePattern().Match(Path.GetFileName(path));
        return match.Success && int.TryParse(match.Groups[1].Value, out var scale) ? scale : 100;
    }

    [GeneratedRegex(@"\.scale-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ScalePattern();

    [GeneratedRegex(@"\.targetsize-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TargetSizePattern();
}