using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class StartAppScannerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AppsFolderIncludesPrimaryLaunchers(int launcherKind)
    {
        Assert.True(StartAppScanner.IsAppsFolderLauncher(
            "Calculator",
            "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            null,
            launcherKind));
    }

    [Fact]
    public void AppsFolderExcludesUnavailableClickToDoSystemEntry()
    {
        Assert.True(StartAppScanner.IsExcludedAppsFolderSystemEntry(
            "MicrosoftWindows.Client.CoreAI_cw5n1h2txyewy!ClickToDoApp"));
    }

    [Fact]
    public void AppsFolderKeepsNormalPackagedApplications()
    {
        Assert.False(StartAppScanner.IsExcludedAppsFolderSystemEntry(
            "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"));
    }

    [Theory]
    [InlineData(null, "App", null, 1)]
    [InlineData("App", null, null, 1)]
    [InlineData("App", "App.Id", null, 0)]
    [InlineData("App", "App.Id", "Parent.Id", 1)]
    [InlineData("Desktop", "Microsoft.Windows.Desktop", null, 1)]
    public void AppsFolderExcludesItemsThatAreNotPrimaryLaunchers(
        string? name,
        string? appUserModelId,
        string? parentAppUserModelId,
        int launcherKind)
    {
        Assert.False(StartAppScanner.IsAppsFolderLauncher(
            name,
            appUserModelId,
            parentAppUserModelId,
            launcherKind));
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
