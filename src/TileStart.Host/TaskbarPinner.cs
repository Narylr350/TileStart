using System.IO;
using System.Runtime.InteropServices;

namespace TileStart.Host;

public static class TaskbarPinner
{
    private static readonly Guid TaskbandPinClass = new("90AA3A4E-1CBA-4233-B8BB-535773D48449");
    private static readonly Guid PinnedList3Interface = new("0DD79AE2-D156-45D4-9EEB-3B549769E940");

    public static bool CanPin(AppEntry app) =>
        !app.IsFolder
        && (!string.IsNullOrWhiteSpace(app.AppUserModelId)
            || IsClassicShortcut(app.LaunchTarget));

    public static bool CanPin(TileItem tile) =>
        tile.TargetType == TileTargetType.Application
        && (tile.LaunchTarget.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase)
            || IsClassicShortcut(tile.LaunchTarget)
            || IsExecutable(tile.LaunchTarget));

    public static Task<bool> RequestPinAsync(AppEntry app)
    {
        var displayName = !string.IsNullOrWhiteSpace(app.AppUserModelId)
            ? $"shell:AppsFolder\\{app.AppUserModelId}"
            : app.LaunchTarget;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => completion.SetResult(PinDisplayName(displayName)))
        {
            IsBackground = true,
            Name = "TileStart Taskbar Pin",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    public static Task<bool> RequestPinAsync(TileItem tile) =>
        RequestPinDisplayNameAsync(tile.LaunchTarget);

    internal static bool IsClassicShortcut(string launchTarget) =>
        File.Exists(launchTarget)
        && Path.GetExtension(launchTarget).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    internal static bool IsExecutable(string launchTarget) =>
        File.Exists(launchTarget)
        && Path.GetExtension(launchTarget).Equals(".exe", StringComparison.OrdinalIgnoreCase);

    private static Task<bool> RequestPinDisplayNameAsync(string displayName)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => completion.SetResult(PinDisplayName(displayName)))
        {
            IsBackground = true,
            Name = "TileStart Taskbar Pin",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static bool PinDisplayName(string displayName)
    {
        nint itemIdList = 0;
        nint pinnedList = 0;
        try
        {
            var result = SHParseDisplayName(displayName, 0, out itemIdList, 0, out _);
            if (result < 0 || itemIdList == 0)
            {
                return false;
            }

            var classId = TaskbandPinClass;
            var interfaceId = PinnedList3Interface;
            result = CoCreateInstance(ref classId, 0, 1, ref interfaceId, out pinnedList);
            if (result < 0 || pinnedList == 0)
            {
                return false;
            }

            var vtable = Marshal.ReadIntPtr(pinnedList);
            var modifyPointer = Marshal.ReadIntPtr(vtable, 16 * IntPtr.Size);
            var modify = Marshal.GetDelegateForFunctionPointer<ModifyDelegate>(modifyPointer);
            return modify(pinnedList, 0, itemIdList, int.MaxValue) >= 0;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Taskbar pin request failed: target={displayName}, error={exception}");
            return false;
        }
        finally
        {
            if (pinnedList != 0)
            {
                Marshal.Release(pinnedList);
            }

            if (itemIdList != 0)
            {
                Marshal.FreeCoTaskMem(itemIdList);
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ModifyDelegate(nint instance, nint unpin, nint pin, int caller);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(
        string name,
        nint bindingContext,
        out nint itemIdList,
        uint attributesIn,
        out uint attributesOut);

    [DllImport("ole32.dll", PreserveSig = true)]
    private static extern int CoCreateInstance(
        ref Guid classId,
        nint outer,
        uint classContext,
        ref Guid interfaceId,
        out nint instance);
}
