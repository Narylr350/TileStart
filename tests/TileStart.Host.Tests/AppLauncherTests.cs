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
