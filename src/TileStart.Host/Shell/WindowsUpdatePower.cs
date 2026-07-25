using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TileStart.Host.Utilities;

namespace TileStart.Host.Shell;

internal static class WindowsUpdatePower
{
    internal const uint ShutdownRestart = 0x00000004;
    internal const uint ShutdownPowerOff = 0x00000008;
    internal const uint ShutdownInstallUpdates = 0x00000040;

    private const uint TokenQuery = 0x0008;
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint SePrivilegeEnabled = 0x00000002;
    private const int ErrorNotAllAssigned = 1300;
    private const uint PlannedOperatingSystemUpgradeReason = 0x80020003;
    private const string ShutdownPrivilege = "SeShutdownPrivilege";

    public static bool IsRestartRequired() =>
        ReadRestartRequired(
            () => CreateComObject("Microsoft.Update.SystemInfo"),
            () => CreateComObject("Microsoft.Update.Session"));

    internal static bool ReadRestartRequired(
        Func<object?> createSystemInfo,
        Func<object?> createUpdateSession) =>
        ReadRestartRequired(createSystemInfo)
        || ReadPendingUpdateRestartRequired(createUpdateSession);

    internal static bool ReadRestartRequired(Func<object?> createSystemInfo)
    {
        object? systemInfo = null;
        try
        {
            systemInfo = createSystemInfo();
            if (systemInfo is null)
            {
                return false;
            }

            return systemInfo.GetType().InvokeMember(
                "RebootRequired",
                BindingFlags.GetProperty,
                binder: null,
                target: systemInfo,
                args: null) is true;
        }
        catch (Exception exception) when (exception is COMException or TargetInvocationException
                                                    or InvalidComObjectException or MemberAccessException)
        {
            DiagnosticLog.Write($"Unable to query Windows Update restart state: {exception}");
            return false;
        }
        finally
        {
            if (systemInfo is not null && Marshal.IsComObject(systemInfo))
            {
                Marshal.FinalReleaseComObject(systemInfo);
            }
        }
    }

    internal static bool ReadPendingUpdateRestartRequired(Func<object?> createUpdateSession)
    {
        object? session = null;
        object? searcher = null;
        object? result = null;
        object? updates = null;
        object? update = null;
        try
        {
            session = createUpdateSession();
            if (session is null)
            {
                return false;
            }

            searcher = session.GetType().InvokeMember(
                "CreateUpdateSearcher",
                BindingFlags.InvokeMethod,
                binder: null,
                target: session,
                args: null);
            if (searcher is null)
            {
                return false;
            }

            searcher.GetType().InvokeMember(
                "Online",
                BindingFlags.SetProperty,
                binder: null,
                target: searcher,
                args: [false]);
            result = searcher.GetType().InvokeMember(
                "Search",
                BindingFlags.InvokeMethod,
                binder: null,
                target: searcher,
                args: ["IsInstalled=0 and IsHidden=0"]);
            updates = result?.GetType().InvokeMember(
                "Updates",
                BindingFlags.GetProperty,
                binder: null,
                target: result,
                args: null);
            if (updates is null)
            {
                return false;
            }

            var count = updates.GetType().InvokeMember(
                "Count",
                BindingFlags.GetProperty,
                binder: null,
                target: updates,
                args: null) is int value
                ? value
                : 0;
            for (var index = 0; index < count; index++)
            {
                update = updates.GetType().InvokeMember(
                    "Item",
                    BindingFlags.GetProperty | BindingFlags.InvokeMethod,
                    binder: null,
                    target: updates,
                    args: [index]);
                var rebootRequired = update?.GetType().InvokeMember(
                    "RebootRequired",
                    BindingFlags.GetProperty,
                    binder: null,
                    target: update,
                    args: null) is true;
                ReleaseComObject(ref update);
                if (rebootRequired)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (exception is COMException or TargetInvocationException
                                                    or InvalidComObjectException or MemberAccessException
                                                    or MissingMethodException)
        {
            DiagnosticLog.Write($"Unable to query pending Windows Updates: {exception}");
            return false;
        }
        finally
        {
            ReleaseComObject(ref update);
            ReleaseComObject(ref updates);
            ReleaseComObject(ref result);
            ReleaseComObject(ref searcher);
            ReleaseComObject(ref session);
        }
    }

    public static bool TryInstallUpdatesAndShutDown(bool restart)
    {
        try
        {
            if (!EnableShutdownPrivilege())
            {
                return false;
            }

            var result = InitiateShutdown(
                machineName: null,
                message: null,
                gracePeriod: 0,
                shutdownFlags: ShutdownInstallUpdates | (restart ? ShutdownRestart : ShutdownPowerOff),
                reason: PlannedOperatingSystemUpgradeReason);
            if (result == 0)
            {
                return true;
            }

            DiagnosticLog.Write($"Unable to initiate Windows Update shutdown: error={result}.");
            return false;
        }
        catch (Exception exception) when (exception is Win32Exception or DllNotFoundException
                                                    or EntryPointNotFoundException)
        {
            DiagnosticLog.Write($"Unable to initiate Windows Update shutdown: {exception}");
            return false;
        }
    }

    internal static uint ShutdownFlags(bool restart) =>
        ShutdownInstallUpdates | (restart ? ShutdownRestart : ShutdownPowerOff);

    private static object? CreateComObject(string programmaticId)
    {
        var type = Type.GetTypeFromProgID(programmaticId, throwOnError: false);
        return type is null ? null : Activator.CreateInstance(type);
    }

    private static void ReleaseComObject(ref object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }

        value = null;
    }

    private static bool EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery | TokenAdjustPrivileges, out var token))
        {
            DiagnosticLog.Write($"Unable to open process token for shutdown: error={Marshal.GetLastWin32Error()}.");
            return false;
        }

        using (token)
        {
            if (!LookupPrivilegeValue(null, ShutdownPrivilege, out var privilege))
            {
                DiagnosticLog.Write($"Unable to resolve shutdown privilege: error={Marshal.GetLastWin32Error()}.");
                return false;
            }

            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Privileges = new LuidAndAttributes
                {
                    Luid = privilege,
                    Attributes = SePrivilegeEnabled,
                },
            };
            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
            {
                DiagnosticLog.Write($"Unable to enable shutdown privilege: error={Marshal.GetLastWin32Error()}.");
                return false;
            }

            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotAllAssigned)
            {
                DiagnosticLog.Write("Unable to enable shutdown privilege: privilege is not assigned.");
                return false;
            }

            return true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LuidAndAttributes
    {
        public Luid Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public LuidAndAttributes Privileges;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        SafeAccessTokenHandle tokenHandle,
        bool disableAllPrivileges,
        ref TokenPrivileges newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint InitiateShutdown(
        string? machineName,
        string? message,
        uint gracePeriod,
        uint shutdownFlags,
        uint reason);
}
