using System.IO;
using TileStart.Host.Applications;
using TileStart.Host.Controllers;

namespace TileStart.Host.Tests;

public sealed class ApplicationPaneRefreshTests
{
    [Fact]
    public void IdenticalApplicationTreesDoNotForceACollectionRebuild()
    {
        var addedAt = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Local);
        var current = new[]
        {
            AppEntry.Folder("Tools", [AppEntry.Application("Editor", @"C:\Apps\Editor.lnk", addedAt)]),
            AppEntry.Application("Store App", @"shell:AppsFolder\Package!App", DateTime.MinValue,
                packageInstallPath: @"C:\Program Files\WindowsApps\Package_1.0",
                appUserModelId: "Package!App"),
        };
        var scanned = new[]
        {
            AppEntry.Folder("Tools", [AppEntry.Application("Editor", @"C:\Apps\Editor.lnk", addedAt)]),
            AppEntry.Application("Store App", @"shell:AppsFolder\Package!App", DateTime.MinValue,
                packageInstallPath: @"C:\Program Files\WindowsApps\Package_1.0",
                appUserModelId: "Package!App"),
        };

        Assert.True(ApplicationPaneController.ApplicationTreesMatch(current, scanned));
    }

    [Fact]
    public void NewlyInstalledApplicationInvalidatesTheCachedTree()
    {
        var current = new[] { AppEntry.Application("Existing", @"C:\Apps\Existing.lnk", DateTime.MinValue) };
        var scanned = new[]
        {
            AppEntry.Application("Existing", @"C:\Apps\Existing.lnk", DateTime.MinValue),
            AppEntry.Application("New App", @"C:\Apps\New App.lnk", DateTime.Now),
        };

        Assert.False(ApplicationPaneController.ApplicationTreesMatch(current, scanned));
    }

    [Fact]
    public void PackageUpdateAndFolderChildChangesInvalidateTheCachedTree()
    {
        var currentPackage = AppEntry.Application("Store App", @"shell:AppsFolder\Package!App", DateTime.MinValue,
            packageInstallPath: @"C:\Program Files\WindowsApps\Package_1.0",
            appUserModelId: "Package!App");
        var updatedPackage = AppEntry.Application("Store App", @"shell:AppsFolder\Package!App", DateTime.MinValue,
            packageInstallPath: @"C:\Program Files\WindowsApps\Package_2.0",
            appUserModelId: "Package!App");
        var currentFolder = AppEntry.Folder("Tools",
            [AppEntry.Application("Editor", @"C:\Apps\Editor.lnk", DateTime.MinValue)]);
        var updatedFolder = AppEntry.Folder("Tools",
            [
                AppEntry.Application("Editor", @"C:\Apps\Editor.lnk", DateTime.MinValue),
                AppEntry.Application("Terminal", @"C:\Apps\Terminal.lnk", DateTime.MinValue),
            ]);

        Assert.False(ApplicationPaneController.ApplicationTreesMatch([currentPackage], [updatedPackage]));
        Assert.False(ApplicationPaneController.ApplicationTreesMatch([currentFolder], [updatedFolder]));
    }

    [Fact]
    public void ShowingTheStartWindowDoesNotTriggerAnApplicationRescan()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "HostSource",
            "MainWindow.xaml.cs"));

        Assert.DoesNotContain("_appController.RefreshAppsAsync()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckAndRemoveMissingApps", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationChangesAreMonitoredOutsideTheMenuShowPath()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "HostSource",
            "Controllers",
            "ApplicationPaneController.cs"));

        Assert.Contains("new FileSystemWatcher(directory)", source, StringComparison.Ordinal);
        Assert.Contains("PeriodicTimer(PackagedAppRefreshInterval)", source, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.ApplicationIdle", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationEnumerationUsesLowestPriorityWorkerThreads()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "HostSource",
            "Applications",
            "StartAppScanner.cs"));

        Assert.Equal(2, source.Split("Priority = ThreadPriority.Lowest", StringSplitOptions.None).Length - 1);
    }
}
