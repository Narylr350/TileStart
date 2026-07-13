using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace TileStart.Host;

public sealed class ShellIntegrationManager : IDisposable
{
    private Process? _watcher;

    public bool Start()
    {
        var injectorPath = FindNativeFile("TileStart.Injector.exe");
        var hookPath = FindNativeFile("TileStart.ShellHook.dll");
        if (injectorPath is null || hookPath is null)
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = injectorPath,
            WorkingDirectory = Path.GetDirectoryName(injectorPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--watch");
        startInfo.ArgumentList.Add(hookPath);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        try
        {
            _watcher = Process.Start(startInfo);
            return _watcher is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }

    private static string? FindNativeFile(string fileName)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var releasePath = Path.Combine(directory.FullName, "artifacts", "Release", "x64", fileName);
            if (File.Exists(releasePath))
            {
                return releasePath;
            }
        }

        return null;
    }
}
