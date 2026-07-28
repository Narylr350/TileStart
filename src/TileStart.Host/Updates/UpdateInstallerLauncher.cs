using System.Diagnostics;
using System.IO;

namespace TileStart.Host.Updates;

internal static class UpdateInstallerLauncher
{
    private const string InstallerPathVariable = "TILESTART_UPDATE_PATH";
    private const string HostProcessIdVariable = "TILESTART_UPDATE_PID";
    private const string LaunchScript =
        "$hostProcessId = [int]$env:TILESTART_UPDATE_PID; " +
        "Wait-Process -Id $hostProcessId -ErrorAction SilentlyContinue; " +
        "$installerProcess = Start-Process -FilePath $env:TILESTART_UPDATE_PATH -Wait -PassThru; " +
        "if ($installerProcess.ExitCode -eq 0) { " +
        "Remove-Item -LiteralPath $env:TILESTART_UPDATE_PATH -Force -ErrorAction SilentlyContinue; " +
        "$updateDirectory = Split-Path -Parent $env:TILESTART_UPDATE_PATH; " +
        "if (Test-Path -LiteralPath $updateDirectory -PathType Container -and " +
        "((Get-ChildItem -LiteralPath $updateDirectory -Force | Measure-Object).Count -eq 0)) { " +
        "Remove-Item -LiteralPath $updateDirectory -Force -ErrorAction SilentlyContinue } }";

    public static void LaunchAfterHostExit(string installerPath, int hostProcessId)
    {
        _ = Process.Start(CreateStartInfo(installerPath, hostProcessId))
            ?? throw new InvalidOperationException("无法启动更新安装助手。");
    }

    internal static ProcessStartInfo CreateStartInfo(string installerPath, int hostProcessId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
        if (hostProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hostProcessId));
        }

        var fullInstallerPath = Path.GetFullPath(installerPath);
        var managedUpdateRoot = Path.Combine(Path.GetTempPath(), "TileStart", "updates") + Path.DirectorySeparatorChar;
        if (!fullInstallerPath.StartsWith(managedUpdateRoot, StringComparison.OrdinalIgnoreCase)
            || !fullInstallerPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("更新安装器必须位于 TileStart 临时更新目录中。", nameof(installerPath));
        }

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(fullInstallerPath) ?? Path.GetTempPath(),
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(LaunchScript);
        // 路径不拼进 PowerShell 命令，避免空格和特殊字符改变脚本含义。
        startInfo.Environment[InstallerPathVariable] = fullInstallerPath;
        startInfo.Environment[HostProcessIdVariable] = hostProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return startInfo;
    }
}
