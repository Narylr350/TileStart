using System.IO;
using System.Runtime.InteropServices;

namespace TileStart.Host.Utilities;

internal static class LaunchTargetResolver
{
    private const string AppsFolderPrefix = @"shell:AppsFolder\";
    private static readonly string[] TargetDirectoryWorkingExtensions =
        [".exe", ".bat", ".cmd", ".ps1"];

    // AppsFolder 目标是持久化的 Shell 身份，不能整体改写成路径；这里只为当前运行投影出
    // 已存在的文件系统目标。GUID/UWP 等 namespace 目标仍必须交给 Shell 处理，否则会失去激活能力。
    internal static string ResolveExistingTarget(string target)
    {
        if (!TryResolveExistingTarget(target, out var resolvedTarget))
        {
            return target;
        }

        return resolvedTarget;
    }

    internal static bool TryResolveExistingTarget(string target, out string resolvedTarget)
    {
        resolvedTarget = string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var candidate = GetFileSystemCandidate(target.Trim());
        if (candidate is null || (!File.Exists(candidate) && !Directory.Exists(candidate)))
        {
            return false;
        }

        try
        {
            resolvedTarget = Path.GetFullPath(candidate);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            return false;
        }
    }

    internal static string ResolveDefaultWorkingDirectory(string target)
    {
        var resolvedTarget = ResolveExistingTarget(target);
        if (IsShortcut(resolvedTarget)
            && TryReadShortcut(resolvedTarget, out var shortcutTarget, out var shortcutWorkingDirectory))
        {
            if (IsExistingDirectory(shortcutWorkingDirectory))
            {
                return GetFullPath(shortcutWorkingDirectory);
            }

            resolvedTarget = shortcutTarget;
        }

        return ResolveTargetDirectory(resolvedTarget);
    }

    private static string ResolveTargetDirectory(string target)
    {
        var extension = Path.GetExtension(target);
        if (!TargetDirectoryWorkingExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || !Path.IsPathFullyQualified(target))
        {
            return string.Empty;
        }

        return Path.GetDirectoryName(target) ?? string.Empty;
    }

    private static string? GetFileSystemCandidate(string target)
    {
        if (!target.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        var nestedTarget = target[AppsFolderPrefix.Length..];
        return string.IsNullOrWhiteSpace(nestedTarget) ? null : nestedTarget;
    }

    private static bool IsShortcut(string target) =>
        File.Exists(target)
        && Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    private static bool IsExistingDirectory(string path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    private static string GetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            return path;
        }
    }

    private static bool TryReadShortcut(
        string shortcutPath,
        out string targetPath,
        out string workingDirectory)
    {
        targetPath = string.Empty;
        workingDirectory = string.Empty;
        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            shell = shellType is null ? null : Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return false;
            }

            shortcut = ((dynamic)shell).CreateShortcut(shortcutPath);
            dynamic shortcutApi = shortcut;
            targetPath = (shortcutApi.TargetPath as string ?? string.Empty).Trim();
            workingDirectory = (shortcutApi.WorkingDirectory as string ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(targetPath) || !string.IsNullOrWhiteSpace(workingDirectory);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write(
                $"Shortcut launch metadata resolution failed: shortcut={shortcutPath}, error={exception.Message}");
            return false;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
