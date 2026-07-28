using System.Diagnostics;
using System.IO;
using TileStart.Host.Updates;

namespace TileStart.Host.Tests;

public sealed class UpdateInstallerLauncherTests
{
    [Fact]
    public void WaitsForHostAndCleansOnlySuccessfulManagedInstaller()
    {
        var installerPath = Path.Combine(Path.GetTempPath(), "TileStart", "updates", "v0.1.19", "attempt", "setup.exe");

        var startInfo = UpdateInstallerLauncher.CreateStartInfo(installerPath, 1234);

        Assert.Equal("powershell.exe", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        Assert.Equal(Path.GetFullPath(installerPath), startInfo.Environment["TILESTART_UPDATE_PATH"]);
        Assert.Equal("1234", startInfo.Environment["TILESTART_UPDATE_PID"]);
        Assert.Contains(startInfo.ArgumentList, argument => argument.Contains("Wait-Process", StringComparison.Ordinal));
        Assert.Contains(startInfo.ArgumentList, argument => argument.Contains("-Wait -PassThru", StringComparison.Ordinal));
        Assert.Contains(startInfo.ArgumentList, argument => argument.Contains("ExitCode -eq 0", StringComparison.Ordinal));
        Assert.Contains(startInfo.ArgumentList, argument => argument.Contains("Remove-Item", StringComparison.Ordinal));
        Assert.DoesNotContain(startInfo.ArgumentList,
            (string argument) => argument.Contains(installerPath, StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInstallerOutsideManagedUpdateDirectory()
    {
        var installerPath = Path.Combine(Path.GetTempPath(), "unrelated", "setup.exe");

        Assert.Throws<ArgumentException>(() => UpdateInstallerLauncher.CreateStartInfo(installerPath, 1234));
    }
}
