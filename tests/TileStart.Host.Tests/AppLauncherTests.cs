using System.IO;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class AppLauncherTests
{
    [Fact]
    public void CreateStartInfoAppliesArgumentsWorkingDirectoryAndAdministratorVerb()
    {
        var tile = new TileItem
        {
            Name = "工具",
            LaunchTarget = @"C:\Tools\tool.exe",
            Arguments = "--safe-mode",
            WorkingDirectory = @"C:\Tools",
            RunAsAdministrator = true,
        };

        var startInfo = AppLauncher.CreateStartInfo(tile);

        Assert.Equal(tile.LaunchTarget, startInfo.FileName);
        Assert.Equal("--safe-mode", startInfo.Arguments);
        Assert.Equal(@"C:\Tools", startInfo.WorkingDirectory);
        Assert.Equal("runas", startInfo.Verb);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void CreateStartInfoCanForceAdministratorVerb()
    {
        var tile = new TileItem
        {
            Name = "管理工具",
            LaunchTarget = "tool.exe",
        };

        var startInfo = AppLauncher.CreateStartInfo(tile, true);

        Assert.Equal("runas", startInfo.Verb);
    }

    [Fact]
    public void CreateOpenFileLocationStartInfoSelectsStartMenuShortcut()
    {
        var startInfo = AppLauncher.CreateOpenFileLocationStartInfo(
            @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Tool.lnk");

        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.Equal("/select,\"C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\Tool.lnk\"", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void TileFileLocationResolvesLocalAndAppsFolderTargets()
    {
        var directory = Directory.CreateTempSubdirectory("TileStart-");
        var executable = Path.Combine(directory.FullName, "Tool.exe");
        var shortcut = Path.Combine(directory.FullName, "Tool.lnk");
        File.WriteAllText(executable, string.Empty);
        File.WriteAllText(shortcut, string.Empty);
        var apps = new[] { AppEntry.Application("Tool", shortcut, DateTime.MinValue) };

        try
        {
            Assert.Equal(executable, AppLauncher.ResolveOpenFileLocationTarget(
                new TileItem { LaunchTarget = executable }, []));
            Assert.Equal(shortcut, AppLauncher.ResolveOpenFileLocationTarget(
                new TileItem
                {
                    Name = "Tool",
                    TargetType = TileTargetType.Application,
                    LaunchTarget = $@"shell:AppsFolder\{executable}",
                },
                apps));
            Assert.Equal(executable, AppLauncher.ResolveOpenFileLocationTarget(
                new TileItem
                {
                    Name = "Unlisted Tool",
                    TargetType = TileTargetType.Application,
                    LaunchTarget = $@"shell:AppsFolder\{executable}",
                },
                []));
            Assert.Null(AppLauncher.ResolveOpenFileLocationTarget(
                new TileItem { TargetType = TileTargetType.Application, LaunchTarget = @"shell:AppsFolder\Package!App" },
                []));
            Assert.Null(AppLauncher.ResolveOpenFileLocationTarget(
                new TileItem { LaunchTarget = executable, IsTileFolder = true },
                apps));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void ShellTargetsUseDefaultAssociationOrExplorerFallback()
    {
        var directory = Directory.CreateTempSubdirectory("TileStart-Launch-");
        var associatedFile = Path.Combine(directory.FullName, "notes.txt");
        var unknownFile = Path.Combine(directory.FullName, "notes.unknown-extension");
        var extensionlessFile = Path.Combine(directory.FullName, "README");
        var executable = Path.Combine(directory.FullName, "tool.exe");
        File.WriteAllText(associatedFile, string.Empty);
        File.WriteAllText(unknownFile, string.Empty);
        File.WriteAllText(extensionlessFile, string.Empty);
        File.WriteAllText(executable, string.Empty);

        try
        {
            var folderStartInfo = AppLauncher.CreateShellTargetStartInfo(directory.FullName);
            var associatedStartInfo = AppLauncher.CreateShellTargetStartInfo(
                associatedFile, hasFileAssociation: true);
            var unknownStartInfo = AppLauncher.CreateShellTargetStartInfo(
                unknownFile, hasFileAssociation: false);
            var extensionlessStartInfo = AppLauncher.CreateShellTargetStartInfo(
                extensionlessFile, hasFileAssociation: true);
            var executableStartInfo = AppLauncher.CreateShellTargetStartInfo(
                executable, hasFileAssociation: false);

            Assert.Equal("explorer.exe", folderStartInfo.FileName);
            Assert.Equal($"\"{directory.FullName}\"", folderStartInfo.Arguments);
            Assert.Equal(associatedFile, associatedStartInfo.FileName);
            Assert.Equal("explorer.exe", unknownStartInfo.FileName);
            Assert.Equal($"/select,\"{unknownFile}\"", unknownStartInfo.Arguments);
            Assert.Equal("explorer.exe", extensionlessStartInfo.FileName);
            Assert.Equal($"/select,\"{extensionlessFile}\"", extensionlessStartInfo.Arguments);
            Assert.Equal(executable, executableStartInfo.FileName);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void CreateStartInfoSupportsCustomCommand()
    {
        var tile = new TileItem
        {
            Name = "命令提示符",
            TargetType = TileTargetType.Command,
            LaunchTarget = "cmd.exe",
            Arguments = "/c echo TileStart",
        };

        var startInfo = AppLauncher.CreateStartInfo(tile);

        Assert.Equal("cmd.exe", startInfo.FileName);
        Assert.Equal("/c echo TileStart", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void CreateStartInfoExecutesPowerShellScriptWithConfiguredArguments()
    {
        var tile = new TileItem
        {
            Name = "部署",
            LaunchTarget = @"C:\Scripts\deploy task.ps1",
            Arguments = "-Environment Test",
        };

        var startInfo = AppLauncher.CreateStartInfo(tile);

        Assert.Equal("powershell.exe", startInfo.FileName);
        Assert.Equal("-NoProfile -ExecutionPolicy Bypass -File \"C:\\Scripts\\deploy task.ps1\" -Environment Test", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }
}
