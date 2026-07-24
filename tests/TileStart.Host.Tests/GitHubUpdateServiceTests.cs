using System.IO;
using TileStart.Host.Updates;

namespace TileStart.Host.Tests;

public sealed class GitHubUpdateServiceTests
{
    private const string ReleaseJson = """
                                               {
                                                 "tag_name": "v0.2.0",
                                                 "html_url": "https://github.com/Narylr350/TileStart/releases/tag/v0.2.0",
                                                 "assets": [
                                                   {
                                                     "name": "TileStart-Setup-win-x64.exe",
                                                     "browser_download_url": "https://github.com/Narylr350/TileStart/releases/download/v0.2.0/TileStart-Setup-win-x64.exe"
                                                   },
                                                   {
                                                     "name": "TileStart-portable-win-x64.zip",
                                                     "browser_download_url": "https://github.com/Narylr350/TileStart/releases/download/v0.2.0/TileStart-portable-win-x64.zip"
                                                   },
                                                   {
                                                     "name": "SHA256SUMS.txt",
                                                     "browser_download_url": "https://github.com/Narylr350/TileStart/releases/download/v0.2.0/SHA256SUMS.txt"
                                                   }
                                                 ]
                                               }
                                               """;

    [Fact]
    public void ParsesReleaseAndSelectsPackageForInstallType()
    {
        var release = GitHubUpdateService.ParseRelease(ReleaseJson);

        Assert.Equal(new Version(0, 2, 0, 0), release.Version);
        Assert.Equal(UpdatePackageKind.Installer, GitHubUpdateService.SelectPackage(release, true).Kind);
        Assert.Equal(UpdatePackageKind.PortableArchive, GitHubUpdateService.SelectPackage(release, false).Kind);
    }

    [Theory]
    [InlineData("0.1.0", "0.1.1", true)]
    [InlineData("0.1.1", "0.1.1", false)]
    [InlineData("0.2.0", "0.1.9", false)]
    public void ComparesNormalizedVersions(string current, string available, bool expected)
    {
        Assert.Equal(expected,
            GitHubUpdateService.IsNewer(Version.Parse(current), Version.Parse(available)));
    }

    [Fact]
    public void ReadsExpectedHashForNamedAsset()
    {
        var expected = new string('a', 64);
        var manifest = $"{new string('b', 64)} *other.zip\n{expected} *TileStart-Setup-win-x64.exe\n";

        Assert.Equal(expected,
            GitHubUpdateService.ReadExpectedSha256(manifest, "TileStart-Setup-win-x64.exe"));
    }

    [Fact]
    public void RejectsReleaseWithoutRequiredAsset()
    {
        var json = ReleaseJson.Replace("TileStart-portable-win-x64.zip", "portable.zip", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GitHubUpdateService.ParseRelease(json));
    }

    [Fact]
    public void RejectsAssetsOutsideGitHub()
    {
        var json = ReleaseJson.Replace(
            "https://github.com/Narylr350/TileStart/releases/download/v0.2.0/TileStart-Setup-win-x64.exe",
            "https://example.com/TileStart-Setup-win-x64.exe",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GitHubUpdateService.ParseRelease(json));
    }

    [Fact]
    public void DetectsInstalledCopyFromInnoUninstaller()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tilestart-update-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var executable = Path.Combine(directory, "TileStart.Host.exe");
            File.WriteAllText(executable, string.Empty);
            Assert.False(GitHubUpdateService.IsInstalledCopy(executable));

            File.WriteAllText(Path.Combine(directory, "unins000.exe"), string.Empty);
            Assert.True(GitHubUpdateService.IsInstalledCopy(executable));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
