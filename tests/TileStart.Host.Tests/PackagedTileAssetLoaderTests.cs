using System.IO;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class PackagedTileAssetLoaderTests
{
    [Theory]
    [InlineData(TileSize.Small, "Small.scale-200.png")]
    [InlineData(TileSize.Medium, "Medium.scale-200.png")]
    [InlineData(TileSize.Wide, "Wide.scale-200.png")]
    [InlineData(TileSize.Large, "Large.scale-200.png")]
    public void ResolveAssetPathUsesManifestTileLogoForRequestedSize(TileSize size, string expectedFile)
    {
        var package = CreatePackage();
        try
        {
            var path = PackagedTileAssetLoader.ResolveAssetPath(package, "Example.Package_abc!App", size);

            Assert.Equal(expectedFile, Path.GetFileName(path));
        }
        finally
        {
            Directory.Delete(package, recursive: true);
        }
    }

    [Fact]
    public void ResolveAssetPathPrefersScaleClosestToCurrent150PercentBaseline()
    {
        var package = CreatePackage();
        try
        {
            File.WriteAllText(Path.Combine(package, "Assets", "Medium.scale-125.png"), string.Empty);
            File.WriteAllText(Path.Combine(package, "Assets", "Medium.scale-400.png"), string.Empty);

            var path = PackagedTileAssetLoader.ResolveAssetPath(package, "Example.Package_abc!App", TileSize.Medium);

            Assert.Equal("Medium.scale-125.png", Path.GetFileName(path));
        }
        finally
        {
            Directory.Delete(package, recursive: true);
        }
    }

    private static string CreatePackage()
    {
        var package = Path.Combine(Path.GetTempPath(), "TileStart.Tests", Guid.NewGuid().ToString("N"));
        var assets = Directory.CreateDirectory(Path.Combine(package, "Assets")).FullName;
        File.WriteAllText(Path.Combine(package, "AppxManifest.xml"), """
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
              <Applications>
                <Application Id="App">
                  <uap:VisualElements Square150x150Logo="Assets\Medium.png">
                    <uap:DefaultTile Square71x71Logo="Assets\Small.png"
                                     Wide310x150Logo="Assets\Wide.png"
                                     Square310x310Logo="Assets\Large.png" />
                  </uap:VisualElements>
                </Application>
              </Applications>
            </Package>
            """);
        foreach (var name in new[] { "Small", "Medium", "Wide", "Large" })
        {
            File.WriteAllText(Path.Combine(assets, $"{name}.scale-200.png"), string.Empty);
        }

        return package;
    }
}
