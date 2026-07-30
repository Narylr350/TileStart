using System.IO;

namespace TileStart.Host.Tests;

public sealed class RecentAppsAnimationTests
{
    private static readonly string ControllerSource = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "HostSource",
        "Controllers",
        "TileWorkspaceController.cs");

    [Fact]
    public void RecentAppsToggleReusesTheAppFolderMotion()
    {
        var source = File.ReadAllText(ControllerSource);
        var methodStart = source.IndexOf("public async Task ToggleRecentAppsAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("public async Task ToggleAppFolderAsync", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("SystemParameters.ClientAreaAnimation", method, StringComparison.Ordinal);
        Assert.Contains("CaptureAppEntryPositions", method, StringComparison.Ordinal);
        Assert.Contains("AnimateAppEntryReflowFrom", method, StringComparison.Ordinal);
        Assert.Contains("AnimateAppRows", method, StringComparison.Ordinal);
        Assert.Contains("Win10FolderMotion.AppOpenDuration", method, StringComparison.Ordinal);
        Assert.Contains("Win10FolderMotion.AppChildDurationMilliseconds", method, StringComparison.Ordinal);
        Assert.Contains("Win10FolderMotion.StandardSpline", method, StringComparison.Ordinal);
    }

    [Fact]
    public void RecentAppsButtonRoutesThroughTheAnimatedWorkspacePath()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "HostSource",
            "MainWindow.Applications.cs"));

        Assert.Contains("_tileWorkspaceController.ToggleRecentAppsAsync()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_appController.ToggleRecentApps()", source, StringComparison.Ordinal);
    }
}
