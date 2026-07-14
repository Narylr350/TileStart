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
