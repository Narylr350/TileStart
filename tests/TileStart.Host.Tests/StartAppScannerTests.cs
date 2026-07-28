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

    [Theory]
    [InlineData(@"C:\Users\User\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Tool.lnk", true)]
    [InlineData(@"C:\Users\User\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Nested\Tool.lnk", true)]
    [InlineData(@"C:\Users\User\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup Tools\Tool.lnk", false)]
    [InlineData(@"C:\Users\User\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Tool.lnk", false)]
    public void StartupDirectoryIsExcludedWithoutMatchingSimilarFolderNames(string path, bool expected)
    {
        const string startup =
            @"C:\Users\User\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup";

        Assert.Equal(expected, StartAppScanner.IsExcludedStartMenuShortcut(path, [startup]));
    }

    [Fact]
    public void UserAndCommonStartupDirectoriesAreBothExcluded()
    {
        var excludedDirectories = new[]
        {
            @"C:\Users\User\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup",
            @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup",
        };

        Assert.True(StartAppScanner.IsExcludedStartMenuShortcut(
            @"C:\Users\User\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\UserTool.lnk",
            excludedDirectories));
        Assert.True(StartAppScanner.IsExcludedStartMenuShortcut(
            @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup\MachineTool.lnk",
            excludedDirectories));
    }
}
