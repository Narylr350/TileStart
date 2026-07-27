using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TileStart.Host.Utilities;

public static class DiagnosticBundleService
{
    private const long MaximumLogBytes = 4 * 1024 * 1024;
    private static readonly string[] LogFileNames = ["TileStart.log", "ShellHook.log"];

    public static void Export(string destinationPath)
    {
        DiagnosticLog.Flush();
        Export(destinationPath, DiagnosticLog.DirectoryPath, DateTimeOffset.Now);
    }

    internal static void Export(string destinationPath, string dataDirectory, DateTimeOffset generatedAt)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create);
        AddTextEntry(archive, "system-info.txt", BuildSystemInformation(generatedAt));
        AddTextEntry(archive, "README.txt",
            "此诊断包只收集系统/版本信息与 TileStart 运行日志，不包含布局和图标资源。\r\n" +
            "日志可能包含本地文件路径或应用名称，提交到公开 Issue 前请先检查内容。\r\n");

        foreach (var fileName in LogFileNames)
        {
            AddLogTail(archive, Path.Combine(dataDirectory, fileName), fileName);
        }
    }

    private static string BuildSystemInformation(DateTimeOffset generatedAt)
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(DiagnosticBundleService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
        var processPath = Environment.ProcessPath ?? "unknown";
        var productVersion = File.Exists(processPath)
            ? FileVersionInfo.GetVersionInfo(processPath).ProductVersion ?? informationalVersion
            : informationalVersion;

        var information = new StringBuilder();
        information.AppendLine($"Generated: {generatedAt:O}");
        information.AppendLine($"TileStart product version: {productVersion}");
        information.AppendLine($"TileStart informational version: {informationalVersion}");
        information.AppendLine($"OS description: {RuntimeInformation.OSDescription}");
        information.AppendLine($"OS version: {Environment.OSVersion.Version}");
        information.AppendLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
        information.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        information.AppendLine($"64-bit operating system: {Environment.Is64BitOperatingSystem}");
        information.AppendLine($"Process path: {processPath}");
        information.AppendLine($"Per-log export limit: {MaximumLogBytes} bytes (latest content retained)");
        return information.ToString();
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static void AddLogTail(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (source.Length > MaximumLogBytes)
        {
            source.Seek(-MaximumLogBytes, SeekOrigin.End);
        }

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var target = entry.Open();
        source.CopyTo(target);
    }
}
