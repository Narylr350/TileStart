using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class StartAppScannerTests
{
    [Theory]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe")]
    [InlineData("52295McMullenSoftware.TileGenie_kfbqnnmtpr2vc")]
    public void AppsFolderIncludesPackagedApplications(string packageFamilyName)
    {
        Assert.True(StartAppScanner.IsPackagedAppsFolderItem(packageFamilyName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AppsFolderExcludesClassicShortcuts(string? packageFamilyName)
    {
        Assert.False(StartAppScanner.IsPackagedAppsFolderItem(packageFamilyName));
    }
}
