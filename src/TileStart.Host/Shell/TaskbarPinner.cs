using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TileStart.Host.Applications;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Utilities;

namespace TileStart.Host.Shell;

public static class TaskbarPinner
{
    private static readonly Guid TaskbandPinClass = new("90AA3A4E-1CBA-4233-B8BB-535773D48449");
    private static readonly Guid PinnedList3Interface = new("0DD79AE2-D156-45D4-9EEB-3B549769E940");
    internal static string ShortcutRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        "TileStart Taskbar Pins");

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
        return RequestPinDisplayNameAsync(displayName, app.Name);
    }

    public static Task<bool> RequestPinAsync(TileItem tile) =>
        RequestPinDisplayNameAsync(tile.LaunchTarget, tile.Name);

    internal static bool IsClassicShortcut(string launchTarget) =>
        File.Exists(launchTarget)
        && Path.GetExtension(launchTarget).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    internal static bool IsExecutable(string launchTarget) =>
        File.Exists(launchTarget)
        && Path.GetExtension(launchTarget).Equals(".exe", StringComparison.OrdinalIgnoreCase);

    private static Task<bool> RequestPinDisplayNameAsync(string displayName, string displayNameHint)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var normalizedTarget = NormalizeDisplayName(displayName);
            var pinTarget = PreparePinTarget(normalizedTarget, displayNameHint);
            completion.SetResult(PinDisplayName(pinTarget, pinTarget == normalizedTarget ? null : normalizedTarget));
        })
        {
            IsBackground = true,
            Name = "TileStart Taskbar Pin",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    internal static string NormalizeDisplayName(string displayName)
    {
        const string appsFolderPrefix = "shell:AppsFolder\\";
        if (!displayName.StartsWith(appsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return displayName;
        }

        var nestedTarget = displayName[appsFolderPrefix.Length..];
        return IsExecutable(nestedTarget) || IsClassicShortcut(nestedTarget)
            ? nestedTarget
            : displayName;
    }

    private static string PreparePinTarget(string launchTarget, string displayNameHint)
    {
        if (!IsClassicShortcut(launchTarget) && !IsExecutable(launchTarget))
        {
            return launchTarget;
        }

        object? shell = null;
        object? sourceShortcut = null;
        object? targetShortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            shell = shellType is null ? null : Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return launchTarget;
            }

            string targetPath;
            string arguments;
            string workingDirectory;
            string description;
            string iconLocation;
            if (IsClassicShortcut(launchTarget))
            {
                sourceShortcut = ((dynamic)shell).CreateShortcut(launchTarget);
                dynamic source = sourceShortcut;
                targetPath = source.TargetPath as string ?? string.Empty;
                arguments = source.Arguments as string ?? string.Empty;
                workingDirectory = source.WorkingDirectory as string ?? string.Empty;
                description = source.Description as string ?? string.Empty;
                iconLocation = source.IconLocation as string ?? string.Empty;
            }
            else
            {
                targetPath = launchTarget;
                arguments = string.Empty;
                workingDirectory = Path.GetDirectoryName(launchTarget) ?? string.Empty;
                description = displayNameHint;
                iconLocation = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return launchTarget;
            }

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(launchTarget)))[..12];
            var shortcutDirectory = Path.Combine(ShortcutRoot, hash);
            Directory.CreateDirectory(shortcutDirectory);
            SetHiddenAttribute(ShortcutRoot);
            SetHiddenAttribute(shortcutDirectory);
            var shortcutPath = Path.Combine(shortcutDirectory, CreateShortcutFileName(displayNameHint));
            if (IsClassicShortcut(launchTarget))
            {
                File.Copy(launchTarget, shortcutPath, true);
            }

            targetShortcut = ((dynamic)shell).CreateShortcut(shortcutPath);
            dynamic target = targetShortcut;
            target.TargetPath = targetPath;
            target.Arguments = arguments;
            target.WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Path.GetDirectoryName(targetPath) ?? string.Empty
                : workingDirectory;
            target.Description = string.IsNullOrWhiteSpace(description) ? displayNameHint : description;
            target.IconLocation = HasExplicitIconLocation(iconLocation)
                ? iconLocation
                : $"{targetPath},0";
            target.Save();
            SHChangeNotify(0x00002000, 0x0005, shortcutPath, 0);
            DiagnosticLog.Write(
                $"Taskbar pin target prepared: source={launchTarget}, shortcut={shortcutPath}, icon={target.IconLocation}");
            return shortcutPath;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write($"Taskbar pin shortcut preparation failed: target={launchTarget}, error={exception}");
            return launchTarget;
        }
        finally
        {
            ReleaseComObject(targetShortcut);
            ReleaseComObject(sourceShortcut);
            ReleaseComObject(shell);
        }
    }

    private static string CreateShortcutFileName(string displayName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(displayName
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Application";
        }

        return $"{safeName}.lnk";
    }

    private static void SetHiddenAttribute(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Hidden) == 0)
        {
            File.SetAttributes(path, attributes | FileAttributes.Hidden);
        }
    }

    private static bool HasExplicitIconLocation(string iconLocation)
    {
        var separator = iconLocation.LastIndexOf(',');
        var path = separator >= 0 ? iconLocation[..separator] : iconLocation;
        return !string.IsNullOrWhiteSpace(path.Trim().Trim('"'));
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static bool PinDisplayName(string displayName, string? replaceDisplayName)
    {
        nint itemIdList = 0;
        nint replaceItemIdList = 0;
        nint pinnedList = 0;
        try
        {
            var result = SHParseDisplayName(displayName, 0, out itemIdList, 0, out _);
            if (result < 0 || itemIdList == 0)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(replaceDisplayName))
            {
                SHParseDisplayName(replaceDisplayName, 0, out replaceItemIdList, 0, out _);
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
            var resultCode = modify(pinnedList, replaceItemIdList, itemIdList, int.MaxValue);
            if (resultCode < 0 && replaceItemIdList != 0)
            {
                DiagnosticLog.Write(
                    $"Taskbar pin replacement rejected: target={displayName}, replaced={replaceDisplayName}, hresult=0x{resultCode:X8}; retrying regular pin.");
                resultCode = modify(pinnedList, 0, itemIdList, int.MaxValue);
            }

            var succeeded = resultCode >= 0;
            DiagnosticLog.Write(
                $"Taskbar pin request completed: target={displayName}, replaced={replaceDisplayName ?? "none"}, hresult=0x{resultCode:X8}, succeeded={succeeded}");
            return succeeded;
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


            if (replaceItemIdList != 0)
            {
                Marshal.FreeCoTaskMem(replaceItemIdList);
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint eventId, uint flags, string item1, nint item2);
}