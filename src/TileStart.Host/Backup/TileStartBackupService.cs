using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TileStart.Host.Persistence;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Backup;

[Flags]
public enum BackupComponents
{
    None = 0,
    Layout = 1,
    CustomApplications = 2,
    ApplicationVisibility = 4,
    Preferences = 8,
    ManagedIcons = 16,
    ExternalVisuals = 32,
    TaskbarShortcuts = 64,
    Default = Layout | CustomApplications | ApplicationVisibility | Preferences | ManagedIcons | ExternalVisuals,
    All = Default | TaskbarShortcuts,
}

public sealed record BackupInspection(DateTime CreatedAt, string AppVersion, BackupComponents Components);

public sealed record BackupRestoreRequest(string ArchivePath, BackupComponents Components);

public sealed class TileStartBackupService
{
    internal const int CurrentFormatVersion = 1;
    private const string BackupUriPrefix = "tilestart-backup:///";
    private const long MaximumRestoreBytes = 1024L * 1024 * 1024;
    private const int MaximumRestoreEntries = 10000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _dataRoot;
    private readonly string _iconsRoot;

    public TileStartBackupService(string? dataRoot = null)
    {
        _dataRoot = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");
        _iconsRoot = Path.Combine(_dataRoot, "icons");
    }

    public static TileStartBackupService Default { get; } = new();

    public void Create(string destinationPath, BackupComponents components)
    {
        if (components == BackupComponents.None)
        {
            throw new InvalidOperationException("请至少选择一项备份内容。");
        }

        var fullDestination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
        var temporaryPath = fullDestination + ".tmp";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        var manifest = new BackupManifest
        {
            FormatVersion = CurrentFormatVersion,
            CreatedAtUtc = DateTime.UtcNow,
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            Components = components,
        };
        var addedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assetsBySource = new Dictionary<string, BackupAsset>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                AddLayout(archive, manifest, components, addedEntries, assetsBySource);
                AddFileComponent(archive, components, BackupComponents.CustomApplications,
                    "custom-apps.json", "data/custom-apps.json", addedEntries);
                AddFileComponent(archive, components, BackupComponents.ApplicationVisibility,
                    "hidden-apps.json", "data/hidden-apps.json", addedEntries);
                if (components.HasFlag(BackupComponents.Preferences))
                {
                    AddFile(archive, Path.Combine(_dataRoot, "window.json"), "data/window.json", addedEntries);
                    AddFile(archive, Path.Combine(_dataRoot, "navigation.json"), "data/navigation.json", addedEntries);
                }

                if (components.HasFlag(BackupComponents.ManagedIcons) && Directory.Exists(_iconsRoot))
                {
                    foreach (var file in Directory.EnumerateFiles(_iconsRoot, "*", SearchOption.AllDirectories))
                    {
                        AddAsset(archive, manifest, file, BackupComponents.ManagedIcons, "assets/icons",
                            addedEntries, assetsBySource);
                    }
                }

                if (components.HasFlag(BackupComponents.TaskbarShortcuts))
                {
                    AddDirectory(archive, Path.Combine(_dataRoot, "taskbar-pins"), "data/taskbar-pins", addedEntries);
                }

                WriteTextEntry(archive, "manifest.json", JsonSerializer.Serialize(manifest, JsonOptions));
            }

            File.Move(temporaryPath, fullDestination, true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public BackupInspection Inspect(string archivePath)
    {
        using var archive = OpenValidatedArchive(archivePath);
        var manifest = ReadManifest(archive);
        return new BackupInspection(manifest.CreatedAtUtc.ToLocalTime(), manifest.AppVersion, manifest.Components);
    }

    public string Restore(string archivePath, BackupComponents components, bool createSafetyBackup = true)
    {
        if (components == BackupComponents.None)
        {
            throw new InvalidOperationException("请至少选择一项恢复内容。");
        }

        using var archive = OpenValidatedArchive(archivePath);
        var manifest = ReadManifest(archive);
        components &= manifest.Components;
        if (components == BackupComponents.None)
        {
            throw new InvalidOperationException("备份中不包含所选内容。");
        }

        var safetyBackup = string.Empty;
        if (createSafetyBackup && Directory.Exists(_dataRoot))
        {
            var backupDirectory = Path.Combine(_dataRoot, "backups");
            Directory.CreateDirectory(backupDirectory);
            safetyBackup = Path.Combine(backupDirectory,
                $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.tilestartbackup");
            Create(safetyBackup, BackupComponents.All);
        }

        Directory.CreateDirectory(_dataRoot);
        RestoreSimpleComponent(archive, components, BackupComponents.CustomApplications,
            "data/custom-apps.json", "custom-apps.json");
        RestoreSimpleComponent(archive, components, BackupComponents.ApplicationVisibility,
            "data/hidden-apps.json", "hidden-apps.json");
        if (components.HasFlag(BackupComponents.Preferences))
        {
            RestoreFile(archive, "data/window.json", Path.Combine(_dataRoot, "window.json"));
            RestoreFile(archive, "data/navigation.json", Path.Combine(_dataRoot, "navigation.json"));
        }

        if (components.HasFlag(BackupComponents.ManagedIcons))
        {
            foreach (var asset in manifest.Assets.Where(asset => asset.Component == BackupComponents.ManagedIcons))
            {
                ExtractAsset(archive, asset);
            }
        }

        if (components.HasFlag(BackupComponents.TaskbarShortcuts))
        {
            RestoreDirectory(archive, "data/taskbar-pins/", Path.Combine(_dataRoot, "taskbar-pins"));
        }

        if (components.HasFlag(BackupComponents.Layout))
        {
            RestoreLayout(archive, manifest, components);
        }

        return safetyBackup;
    }

    private void AddLayout(
        ZipArchive archive,
        BackupManifest manifest,
        BackupComponents components,
        HashSet<string> addedEntries,
        Dictionary<string, BackupAsset> assetsBySource)
    {
        if (!components.HasFlag(BackupComponents.Layout))
        {
            return;
        }

        var path = Path.Combine(_dataRoot, "layout.json");
        if (!File.Exists(path))
        {
            return;
        }

        var layout = TileLayoutStore.Deserialize(File.ReadAllText(path));
        if (layout is null)
        {
            throw new InvalidDataException("当前磁贴布局无法读取，备份已取消。");
        }

        foreach (var tile in EnumerateTiles(layout))
        {
            tile.IconPath = CaptureVisualPath(archive, manifest, components, tile.IconPath, addedEntries, assetsBySource);
            tile.BackgroundImagePath = CaptureVisualPath(archive, manifest, components, tile.BackgroundImagePath,
                addedEntries, assetsBySource);
            if (File.Exists(tile.IconSourceValue))
            {
                tile.IconSourceValue = CaptureVisualPath(archive, manifest, components, tile.IconSourceValue,
                    addedEntries, assetsBySource);
            }
        }

        WriteTextEntry(archive, "data/layout.json", TileLayoutStore.Serialize(layout));
    }

    private string CaptureVisualPath(
        ZipArchive archive,
        BackupManifest manifest,
        BackupComponents components,
        string path,
        HashSet<string> addedEntries,
        Dictionary<string, BackupAsset> assetsBySource)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        var isManaged = IsInsideDirectory(fullPath, _iconsRoot);
        var component = isManaged ? BackupComponents.ManagedIcons : BackupComponents.ExternalVisuals;
        if (!components.HasFlag(component))
        {
            return path;
        }

        var asset = AddAsset(archive, manifest, fullPath, component,
            isManaged ? "assets/icons" : "assets/external", addedEntries, assetsBySource);
        return BackupUriPrefix + asset.ArchivePath;
    }

    private BackupAsset AddAsset(
        ZipArchive archive,
        BackupManifest manifest,
        string sourcePath,
        BackupComponents component,
        string archiveDirectory,
        HashSet<string> addedEntries,
        Dictionary<string, BackupAsset> assetsBySource)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (assetsBySource.TryGetValue(fullPath, out var existing))
        {
            return existing;
        }

        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fullPath)))
            .ToLowerInvariant()[..16];
        var safeName = Path.GetFileName(fullPath).Replace(' ', '-');
        var archivePath = $"{archiveDirectory}/{hash}-{safeName}";
        AddFile(archive, fullPath, archivePath, addedEntries);
        var asset = new BackupAsset
        {
            ArchivePath = archivePath,
            OriginalPath = fullPath,
            Component = component,
        };
        assetsBySource.Add(fullPath, asset);
        manifest.Assets.Add(asset);
        return asset;
    }

    private void RestoreLayout(ZipArchive archive, BackupManifest manifest, BackupComponents components)
    {
        var entry = archive.GetEntry("data/layout.json");
        var destination = Path.Combine(_dataRoot, "layout.json");
        if (entry is null)
        {
            DeleteFile(destination);
            return;
        }

        using var reader = new StreamReader(entry.Open());
        var layout = TileLayoutStore.Deserialize(reader.ReadToEnd())
                     ?? throw new InvalidDataException("备份中的磁贴布局无效。");
        var assets = manifest.Assets.ToDictionary(
            asset => BackupUriPrefix + asset.ArchivePath,
            StringComparer.OrdinalIgnoreCase);
        foreach (var tile in EnumerateTiles(layout))
        {
            tile.IconPath = RestoreVisualPath(archive, components, assets, tile.IconPath);
            tile.BackgroundImagePath = RestoreVisualPath(archive, components, assets, tile.BackgroundImagePath);
            if (tile.IconSourceValue.StartsWith(BackupUriPrefix, StringComparison.OrdinalIgnoreCase))
            {
                tile.IconSourceValue = RestoreVisualPath(archive, components, assets, tile.IconSourceValue);
            }
        }

        WriteAtomic(destination, TileLayoutStore.Serialize(layout));
    }

    private string RestoreVisualPath(
        ZipArchive archive,
        BackupComponents components,
        IReadOnlyDictionary<string, BackupAsset> assets,
        string value)
    {
        if (!assets.TryGetValue(value, out var asset))
        {
            return value;
        }

        return components.HasFlag(asset.Component) ? ExtractAsset(archive, asset) : asset.OriginalPath;
    }

    private string ExtractAsset(ZipArchive archive, BackupAsset asset)
    {
        var entry = archive.GetEntry(asset.ArchivePath)
                    ?? throw new InvalidDataException($"备份资源缺失：{asset.ArchivePath}");
        Directory.CreateDirectory(_iconsRoot);
        var destination = Path.Combine(_iconsRoot, Path.GetFileName(asset.ArchivePath));
        ExtractEntry(entry, destination);
        return destination;
    }

    private void AddFileComponent(
        ZipArchive archive,
        BackupComponents selected,
        BackupComponents component,
        string sourceName,
        string archivePath,
        HashSet<string> addedEntries)
    {
        if (selected.HasFlag(component))
        {
            AddFile(archive, Path.Combine(_dataRoot, sourceName), archivePath, addedEntries);
        }
    }

    private static void AddFile(
        ZipArchive archive,
        string sourcePath,
        string archivePath,
        HashSet<string> addedEntries)
    {
        if (!File.Exists(sourcePath) || !addedEntries.Add(archivePath))
        {
            return;
        }

        archive.CreateEntryFromFile(sourcePath, archivePath, CompressionLevel.Optimal);
    }

    private static void AddDirectory(
        ZipArchive archive,
        string sourceDirectory,
        string archiveDirectory,
        HashSet<string> addedEntries)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            AddFile(archive, file, $"{archiveDirectory}/{relative}", addedEntries);
        }
    }

    private void RestoreSimpleComponent(
        ZipArchive archive,
        BackupComponents selected,
        BackupComponents component,
        string archivePath,
        string destinationName)
    {
        if (selected.HasFlag(component))
        {
            RestoreFile(archive, archivePath, Path.Combine(_dataRoot, destinationName));
        }
    }

    private static void RestoreFile(ZipArchive archive, string archivePath, string destinationPath)
    {
        var entry = archive.GetEntry(archivePath);
        if (entry is null)
        {
            DeleteFile(destinationPath);
            return;
        }

        ExtractEntry(entry, destinationPath);
    }

    private static void RestoreDirectory(ZipArchive archive, string prefix, string destinationRoot)
    {
        if (Directory.Exists(destinationRoot))
        {
            Directory.Delete(destinationRoot, true);
        }

        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal)
                                                             && !string.IsNullOrEmpty(entry.Name)))
        {
            var relative = entry.FullName[prefix.Length..];
            var destination = SafeCombine(destinationRoot, relative);
            ExtractEntry(entry, destination);
        }
    }

    private static void ExtractEntry(ZipArchiveEntry entry, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temporaryPath = destinationPath + ".restore-tmp";
        entry.ExtractToFile(temporaryPath, true);
        File.Move(temporaryPath, destinationPath, true);
    }

    private static void WriteAtomic(string destinationPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temporaryPath = destinationPath + ".restore-tmp";
        File.WriteAllText(temporaryPath, content);
        File.Move(temporaryPath, destinationPath, true);
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static IEnumerable<TileItem> EnumerateTiles(TileLayout layout)
    {
        foreach (var tile in layout.Groups.SelectMany(group => group.Tiles))
        {
            yield return tile;
            foreach (var child in EnumerateFolderTiles(tile))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<TileItem> EnumerateFolderTiles(TileItem tile)
    {
        foreach (var child in tile.FolderTiles)
        {
            yield return child;
            foreach (var descendant in EnumerateFolderTiles(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsInsideDirectory(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar)
               && !Path.IsPathRooted(relative);
    }

    private static string SafeCombine(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("备份包含不安全的文件路径。");
        }

        return fullPath;
    }

    private static ZipArchive OpenValidatedArchive(string archivePath)
    {
        var archive = ZipFile.OpenRead(Path.GetFullPath(archivePath));
        if (archive.Entries.Count > MaximumRestoreEntries
            || archive.Entries.Sum(entry => entry.Length) > MaximumRestoreBytes)
        {
            archive.Dispose();
            throw new InvalidDataException("备份文件过大或包含过多文件。");
        }

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.Contains("..", StringComparison.Ordinal)
                || Path.IsPathRooted(entry.FullName.Replace('/', Path.DirectorySeparatorChar)))
            {
                archive.Dispose();
                throw new InvalidDataException("备份包含不安全的文件路径。");
            }
        }

        return archive;
    }

    private static BackupManifest ReadManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("manifest.json")
                    ?? throw new InvalidDataException("这不是有效的 TileStart 备份。");
        using var reader = new StreamReader(entry.Open());
        var manifest = JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd(), JsonOptions)
                       ?? throw new InvalidDataException("备份清单无效。");
        if (manifest.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException($"不支持的备份格式版本：{manifest.FormatVersion}。");
        }

        return manifest;
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class BackupManifest
    {
        public int FormatVersion { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string AppVersion { get; set; } = string.Empty;
        public BackupComponents Components { get; set; }
        public List<BackupAsset> Assets { get; set; } = [];
    }

    private sealed class BackupAsset
    {
        public string ArchivePath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public BackupComponents Component { get; set; }
    }
}
