using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class AppUninstallerTests
{
    [Fact]
    public void PackagedAppSettingsUriTargetsItsPackageFamily()
    {
        var uri = AppUninstaller.SettingsUri("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

        Assert.Equal(
            "ms-settings:appsfeatures-app?PFN=Microsoft.WindowsCalculator_8wekyb3d8bbwe",
            uri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("classic-app")]
    public void ClassicAppFallsBackToAppsAndFeatures(string appUserModelId)
    {
        Assert.Equal("ms-settings:appsfeatures", AppUninstaller.SettingsUri(appUserModelId));
    }

    [Fact]
    public void FoldersCannotBeUninstalled()
    {
        var folder = AppEntry.Folder("Tools", []);
        var app = AppEntry.Application("Calculator", "calculator.exe", DateTime.MinValue);

        Assert.False(folder.CanUninstall);
        Assert.True(app.CanUninstall);
    }

    [Fact]
    public void MissingClassicShortcutCannotBePinned()
    {
        Assert.False(TaskbarPinner.IsClassicShortcut(@"C:\missing\Tool.lnk"));
    }
}
