using System.IO;
using System.IO.Compression;
using TileStart.Host.Backup;
using TileStart.Host.Icons;
using TileStart.Host.Persistence;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tests;

public sealed class TileStartBackupServiceTests
{
    [Fact]
    public void CompleteBackupRestoresConfigurationAndPortableVisuals()
    {
        using var test = new TemporaryDirectory();
        var dataRoot = Path.Combine(test.Path, "data");
        var externalRoot = Path.Combine(test.Path, "external");
        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(externalRoot);
        var externalIcon = Path.Combine(externalRoot, "cat.gif");
        File.WriteAllBytes(externalIcon, [1, 2, 3, 4]);
        var managedIcon = Path.Combine(dataRoot, "icons", "network-test.png");
        Directory.CreateDirectory(Path.GetDirectoryName(managedIcon)!);
        File.WriteAllBytes(managedIcon, [5, 6, 7]);

        var layout = new TileLayout
        {
            Groups =
            {
                new TileGroup
                {
                    Name = "Backup",
                    Tiles =
                    {
                        new TileItem
                        {
                            Name = "External",
                            IconPath = externalIcon,
                            IconSourceKind = CustomIconSourceKind.LocalFile,
                            IconSourceValue = externalIcon,
                            BackgroundImagePath = managedIcon,
                            Size = TileSize.Medium,
                        },
                    },
                },
            },
        };
        File.WriteAllText(Path.Combine(dataRoot, "layout.json"), TileLayoutStore.Serialize(layout));
        File.WriteAllText(Path.Combine(dataRoot, "custom-apps.json"), "[{\"Name\":\"Portable\"}]");
        File.WriteAllText(Path.Combine(dataRoot, "window.json"), "{\"Width\":900}");
        File.WriteAllText(Path.Combine(dataRoot, "TileStart.log"), "private diagnostics");
        Directory.CreateDirectory(Path.Combine(dataRoot, "backups"));
        File.WriteAllText(Path.Combine(dataRoot, "backups", "old.zip"), "nested backup");

        var archivePath = Path.Combine(test.Path, "settings.tilestartbackup");
        var service = new TileStartBackupService(dataRoot);
        service.Create(archivePath, BackupComponents.Default);

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("data/layout.json"));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("TileStart.log"));
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("backups/"));
        }

        File.WriteAllText(Path.Combine(dataRoot, "custom-apps.json"), "changed");
        File.Delete(managedIcon);
        service.Restore(archivePath, BackupComponents.Default, createSafetyBackup: false);

        Assert.Equal("[{\"Name\":\"Portable\"}]", File.ReadAllText(Path.Combine(dataRoot, "custom-apps.json")));
        var restoredLayout = TileLayoutStore.Deserialize(File.ReadAllText(Path.Combine(dataRoot, "layout.json")));
        Assert.NotNull(restoredLayout);
        var tile = Assert.Single(Assert.Single(restoredLayout.Groups).Tiles);
        Assert.StartsWith(Path.Combine(dataRoot, "icons"), tile.IconPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.Combine(dataRoot, "icons"), tile.BackgroundImagePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(tile.IconPath));
        Assert.True(File.Exists(tile.BackgroundImagePath));
        Assert.Equal(tile.IconPath, tile.IconSourceValue);
    }

    [Fact]
    public void PartialRestoreLeavesUnselectedComponentsUntouched()
    {
        using var test = new TemporaryDirectory();
        var dataRoot = Path.Combine(test.Path, "data");
        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(Path.Combine(dataRoot, "custom-apps.json"), "backup apps");
        File.WriteAllText(Path.Combine(dataRoot, "window.json"), "backup window");
        var archivePath = Path.Combine(test.Path, "partial.tilestartbackup");
        var service = new TileStartBackupService(dataRoot);
        service.Create(archivePath, BackupComponents.CustomApplications | BackupComponents.Preferences);

        File.WriteAllText(Path.Combine(dataRoot, "custom-apps.json"), "current apps");
        File.WriteAllText(Path.Combine(dataRoot, "window.json"), "current window");
        service.Restore(archivePath, BackupComponents.Preferences, createSafetyBackup: false);

        Assert.Equal("current apps", File.ReadAllText(Path.Combine(dataRoot, "custom-apps.json")));
        Assert.Equal("backup window", File.ReadAllText(Path.Combine(dataRoot, "window.json")));
    }

    [Fact]
    public void InspectRejectsArchiveWithTraversalEntry()
    {
        using var test = new TemporaryDirectory();
        var archivePath = Path.Combine(test.Path, "unsafe.tilestartbackup");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("../outside.txt");
            var manifest = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifest.Open());
            writer.Write("{\"FormatVersion\":1,\"CreatedAtUtc\":\"2026-07-24T00:00:00Z\",\"AppVersion\":\"test\",\"Components\":\"Layout\",\"Assets\":[]}");
        }

        var service = new TileStartBackupService(Path.Combine(test.Path, "data"));
        Assert.Throws<InvalidDataException>(() =>
        {
            service.Inspect(archivePath);
        });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TileStartTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
