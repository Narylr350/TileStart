using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace TileStart.Host.Shell;

public sealed class ShellIntegrationManager : IDisposable
{
    private readonly string _stopEventName = $"Local\\TileStart.Injector.Stop.{Environment.ProcessId}";
    private readonly EventWaitHandle _stopEvent;
    private Process? _watcher;

    public ShellIntegrationManager()
    {
        _stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _stopEventName);
    }

    public bool Start()
    {
        if (_watcher is { HasExited: false })
        {
            return true;
        }

        var injectorPath = FindNativeFile("TileStart.Injector.exe");
        var hookPath = FindNativeFile("TileStart.ShellHook.dll");
        if (injectorPath is null || hookPath is null)
        {
            return false;
        }

        _stopEvent.Reset();
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
        startInfo.ArgumentList.Add(_stopEventName);
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

    public void Stop()
    {
        if (_watcher is null)
        {
            return;
        }

        if (!_watcher.HasExited)
        {
            _stopEvent.Set();
            if (!_watcher.WaitForExit(7000))
            {
                _watcher.Kill();
                _watcher.WaitForExit();
            }
        }

        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        Stop();
        _stopEvent.Dispose();
    }

    private static string? FindNativeFile(string fileName)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
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