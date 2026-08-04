using System.IO;
using System.IO.Compression;

namespace TileStart.Host.Tests;

public sealed class DiagnosticBundleServiceTests
{
    [Fact]
    public void ExportIncludesSystemInformationPrivacyNoticeAndAvailableLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TileStart-Diagnostics-{Guid.NewGuid():N}");
        var dataDirectory = Path.Combine(root, "data");
        var destination = Path.Combine(root, "diagnostics.zip");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, "TileStart.log"), "host-log");
        File.WriteAllText(Path.Combine(dataDirectory, "TileStart.previous.log"), "previous-host-log");
        File.WriteAllText(Path.Combine(dataDirectory, "ShellHook.log"), "hook-log");
        File.WriteAllText(Path.Combine(dataDirectory, "ShellHook.previous.log"), "previous-hook-log");

        try
        {
            DiagnosticBundleService.Export(
                destination,
                dataDirectory,
                new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(8)));

            using var archive = ZipFile.OpenRead(destination);
            Assert.Equal(
                [
                    "README.txt",
                    "ShellHook.log",
                    "ShellHook.previous.log",
                    "system-info.txt",
                    "TileStart.log",
                    "TileStart.previous.log",
                ],
                archive.Entries.Select(entry => entry.FullName).Order().ToArray());
            Assert.Contains("OS version:", ReadEntry(archive, "system-info.txt"), StringComparison.Ordinal);
            Assert.Contains("本地文件路径", ReadEntry(archive, "README.txt"), StringComparison.Ordinal);
            Assert.Equal("host-log", ReadEntry(archive, "TileStart.log"));
            Assert.Equal("previous-host-log", ReadEntry(archive, "TileStart.previous.log"));
            Assert.Equal("hook-log", ReadEntry(archive, "ShellHook.log"));
            Assert.Equal("previous-hook-log", ReadEntry(archive, "ShellHook.previous.log"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }
}