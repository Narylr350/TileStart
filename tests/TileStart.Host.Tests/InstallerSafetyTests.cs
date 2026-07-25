using System.IO;

namespace TileStart.Host.Tests;

public sealed class InstallerSafetyTests
{
    private static readonly string InstallerSource = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Installer",
        "TileStart.iss");

    [Fact]
    public void InstallerNeverLetsRestartManagerCloseExplorerForTheInjectedHook()
    {
        var source = File.ReadAllText(InstallerSource);

        Assert.Contains("CloseApplications=no", source, StringComparison.Ordinal);
        Assert.Contains("RestartApplications=no", source, StringComparison.Ordinal);
        Assert.DoesNotContain("taskkill /IM TileStart.Injector.exe", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallerWaitsForTheHostToFinishUnloadingTheExplorerHook()
    {
        var source = File.ReadAllText(InstallerSource);

        Assert.Contains("CheckForMutexes('Local\\TileStart.Host')", source, StringComparison.Ordinal);
        Assert.Contains("if StopTileStart then", source, StringComparison.Ordinal);
        Assert.Contains("安装已中止", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerAlwaysRegistersLoginStartup()
    {
        var source = File.ReadAllText(InstallerSource);

        var startupRegistration = Assert.Single(source.Split('\n'),
            line => line.Contains("ValueName: \"TileStart\"", StringComparison.Ordinal));
        Assert.DoesNotContain("Tasks:", startupRegistration, StringComparison.Ordinal);
        Assert.DoesNotContain("Name: \"autostart\"", source, StringComparison.Ordinal);
    }
}