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
    public void InstallerTracksContextMenusForAllFilesAndDirectories()
    {
        var source = File.ReadAllText(InstallerSource);

        Assert.Contains("Software\\Classes\\*\\shell\\TileStart.AddToAppList", source, StringComparison.Ordinal);
        Assert.Contains("Software\\Classes\\*\\shell\\TileStart.PinTile", source, StringComparison.Ordinal);
        Assert.Contains("Software\\Classes\\Directory\\shell\\TileStart.AddToAppList", source,
            StringComparison.Ordinal);
        Assert.Contains("Software\\Classes\\Directory\\shell\\TileStart.PinTile", source,
            StringComparison.Ordinal);
        Assert.Contains("SystemFileAssociations\\.exe\\shell\\TileStart.AddToAppList\"; Flags: deletekey",
            source, StringComparison.Ordinal);
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

    [Fact]
    public void InstallerRejectsRootAndForeignNonEmptyDirectories()
    {
        var source = File.ReadAllText(InstallerSource);

        Assert.Contains("AppendDefaultDirName=yes", source, StringComparison.Ordinal);
        Assert.Contains("function ValidateInstallDirectory", source, StringComparison.Ordinal);
        Assert.Contains("IsRootDirectory(InstallDirectory)", source, StringComparison.Ordinal);
        Assert.Contains("not DirectoryIsEmpty(InstallDirectory)", source, StringComparison.Ordinal);
        Assert.Contains("not IsExistingTileStartDirectory(InstallDirectory)", source, StringComparison.Ordinal);
        Assert.Contains("if not ValidateInstallDirectory(Result) then", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UninstallerNeverRecursivelyDeletesTheInstallDirectory()
    {
        var source = File.ReadAllText(InstallerSource);

        Assert.Contains("Type: files; Name: \"{app}\\.tilestart-installation\"", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Type: filesandordirs", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Type: dirifempty", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Name: \"{app}\\*\"", source, StringComparison.OrdinalIgnoreCase);
    }
}