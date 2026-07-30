using System.ComponentModel;
using System.Diagnostics;

namespace TileStart.Host.Shell;

internal static class InteractiveProcessPriority
{
    private static bool _boosted;

    public static void Boost()
    {
        if (_boosted || TrySet(ProcessPriorityClass.AboveNormal))
        {
            _boosted = true;
        }
    }

    public static void Restore()
    {
        if (!_boosted)
        {
            return;
        }

        if (TrySet(ProcessPriorityClass.Normal))
        {
            _boosted = false;
        }
    }

    private static bool TrySet(ProcessPriorityClass priority)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            if (process.PriorityClass != priority)
            {
                process.PriorityClass = priority;
            }

            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }
}
