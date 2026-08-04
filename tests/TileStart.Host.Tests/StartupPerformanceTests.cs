using System.IO;

namespace TileStart.Host.Tests;

public sealed class StartupPerformanceTests
{
    private static string HostSource(params string[] parts) => Path.Combine(
        [AppContext.BaseDirectory, "TestData", "HostSource", .. parts]);

    [Fact]
    public void SavedLayoutIsRestoredBeforeApplicationEnumerationStarts()
    {
        var source = File.ReadAllText(HostSource("MainWindow.xaml.cs"));
        var restore = source.IndexOf("_appController.RestoreSavedLayout();", StringComparison.Ordinal);
        var scan = source.IndexOf("_appController.LoadAppsAsync();", StringComparison.Ordinal);

        Assert.True(restore >= 0 && restore < scan);
    }

    [Fact]
    public void TileVisualsLoadOffTheUiThreadAfterContentBecomesUsable()
    {
        var source = File.ReadAllText(HostSource("Controllers", "ApplicationPaneController.cs"));
        var ready = source.IndexOf("_applicationContentReady = true;", StringComparison.Ordinal);
        var visuals = source.IndexOf("_ = LoadTileVisualsAsync(launchableApps);", StringComparison.Ordinal);

        Assert.True(ready >= 0 && ready < visuals);
        Assert.Contains("RunStaThreadAsync(", source, StringComparison.Ordinal);
        Assert.Contains("() => LoadTileVisuals", source, StringComparison.Ordinal);
        Assert.Contains("Priority = ThreadPriority.Lowest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedTilesBeginRestoringVisualsBeforeApplicationEnumerationCompletes()
    {
        var source = File.ReadAllText(HostSource("Controllers", "ApplicationPaneController.cs"));
        var layoutReady = source.IndexOf("Tile layout ready:", StringComparison.Ordinal);
        var initialVisuals = source.IndexOf("_ = LoadTileVisualsAsync([]);", StringComparison.Ordinal);
        var applicationScan = source.IndexOf("var apps = await ScanApplicationsAsync();", StringComparison.Ordinal);

        Assert.True(layoutReady >= 0 && layoutReady < initialVisuals);
        Assert.True(initialVisuals < applicationScan);
    }

    [Fact]
    public void TileVisualBatchBuildsOneIdentityIndexForTheWholeTree()
    {
        var source = File.ReadAllText(HostSource("Controllers", "ApplicationPaneController.cs"));

        Assert.Contains("var appsByIdentity = BuildApplicationIdentityIndex(apps);", source,
            StringComparison.Ordinal);
        Assert.Contains("LoadTileVisualTree(tile, appsByIdentity, loadedVisuals);", source,
            StringComparison.Ordinal);
        Assert.Contains("LoadTileVisualTree(child, appsByIdentity, loadedVisuals);", source,
            StringComparison.Ordinal);

        var singleRestoreStart = source.IndexOf("public static void RestoreTileIcon(", StringComparison.Ordinal);
        var nextMethod = source.IndexOf("private static (ImageSource Icon", singleRestoreStart,
            StringComparison.Ordinal);
        var singleRestore = source[singleRestoreStart..nextMethod];
        Assert.Contains("LoadTileIcon(tile, apps)", singleRestore, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildApplicationIdentityIndex", singleRestore, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationIdentityIndexKeepsTheFirstScannedDuplicate()
    {
        var first = AppEntry.Application(
            "First",
            "shell:AppsFolder\\Example.Package_abc!App",
            DateTime.MinValue);
        var duplicate = AppEntry.Application(
            "Duplicate",
            "SHELL:APPSFOLDER\\EXAMPLE.PACKAGE_ABC!APP",
            DateTime.MinValue);

        var index = TileStart.Host.Controllers.ApplicationPaneController.BuildApplicationIdentityIndex(
            [first, duplicate]);

        Assert.Single(index);
        Assert.Same(first, index[LaunchTargetIdentity.GetKey(first.LaunchTarget)]);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, false)]
    public void OlderTileVisualBatchCannotOverwriteANewerBatch(
        int generation,
        int appliedGeneration,
        bool expected)
    {
        Assert.Equal(
            expected,
            TileStart.Host.Controllers.ApplicationPaneController.ShouldApplyTileVisualGeneration(
                generation,
                appliedGeneration));
    }

    [Fact]
    public void StartWindowBoostsOnlyWhileInteractive()
    {
        var controller = File.ReadAllText(HostSource("Shell", "StartWindowController.cs"));
        var priority = File.ReadAllText(HostSource("Shell", "InteractiveProcessPriority.cs"));

        Assert.Contains("InteractiveProcessPriority.Boost();", controller, StringComparison.Ordinal);
        Assert.True(controller.Split("InteractiveProcessPriority.Restore();", StringSplitOptions.None).Length - 1 >= 3);
        Assert.Contains("ProcessPriorityClass.AboveNormal", priority, StringComparison.Ordinal);
        Assert.Contains("ProcessPriorityClass.Normal", priority, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessPriorityClass.High", priority, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessPriorityClass.RealTime", priority, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginStartupMigratesToAnImmediateNormalPriorityTaskWithRunFallback()
    {
        var registration = File.ReadAllText(HostSource("Shell", "StartupRegistration.cs"));
        var app = File.ReadAllText(HostSource("App.xaml.cs"));

        Assert.Contains("Schedule.Service", registration, StringComparison.Ordinal);
        Assert.Contains("TaskTriggerLogon = 9", registration, StringComparison.Ordinal);
        Assert.Contains("TaskPriorityNormal = 4", registration, StringComparison.Ordinal);
        Assert.Contains("TaskLogonInteractiveToken = 3", registration, StringComparison.Ordinal);
        Assert.Contains("return SetLegacyRunEnabled(true);", registration, StringComparison.Ordinal);
        Assert.Contains("StartupRegistration.MigrateLegacyRegistration()", app, StringComparison.Ordinal);
    }
}
