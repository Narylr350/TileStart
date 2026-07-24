using System.IO;
using System.Text.Json;
using TileStart.Host.Utilities;

namespace TileStart.Host.Applications;

public sealed class CustomAppDefinition
{
    public string Name { get; set; } = string.Empty;
    public string LaunchTarget { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
}

public static class CustomAppStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TileStart",
        "custom-apps.json");

    private static readonly string[] SupportedExtensions = [".exe", ".lnk", ".appref-ms"];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IReadOnlyList<AppEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            return (JsonSerializer.Deserialize<List<CustomAppDefinition>>(File.ReadAllText(FilePath)) ?? [])
                .Where(IsValid)
                .Select(ToAppEntry)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Unable to load custom applications: {exception.Message}");
            return [];
        }
    }

    public static AppEntry? Add(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null)
        {
            return null;
        }

        try
        {
            var definitions = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<CustomAppDefinition>>(File.ReadAllText(FilePath)) ?? []
                : [];
            var identity = LaunchTargetIdentity.GetKey(normalizedPath);
            var existing = definitions.FirstOrDefault(item =>
                LaunchTargetIdentity.GetKey(item.LaunchTarget) == identity);
            if (existing is not null)
            {
                return ToAppEntry(existing);
            }

            var definition = new CustomAppDefinition
            {
                Name = Path.GetFileNameWithoutExtension(normalizedPath),
                LaunchTarget = normalizedPath,
                AddedAt = DateTime.Now,
            };
            definitions.Add(definition);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(definitions, JsonOptions));
            File.Move(temporaryPath, FilePath, true);
            return ToAppEntry(definition);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Unable to add custom application '{path}': {exception.Message}");
            return null;
        }
    }

    public static bool Contains(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null || !File.Exists(FilePath))
        {
            return false;
        }

        try
        {
            var identity = LaunchTargetIdentity.GetKey(normalizedPath);
            return (JsonSerializer.Deserialize<List<CustomAppDefinition>>(File.ReadAllText(FilePath)) ?? [])
                .Any(item => LaunchTargetIdentity.GetKey(item.LaunchTarget) == identity);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Unable to inspect custom applications: {exception.Message}");
            return false;
        }
    }

    public static bool Remove(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null || !File.Exists(FilePath))
        {
            return false;
        }

        try
        {
            var definitions = JsonSerializer.Deserialize<List<CustomAppDefinition>>(File.ReadAllText(FilePath)) ?? [];
            var identity = LaunchTargetIdentity.GetKey(normalizedPath);
            var removed = definitions.RemoveAll(item => LaunchTargetIdentity.GetKey(item.LaunchTarget) == identity) > 0;
            if (!removed)
            {
                return false;
            }

            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(definitions, JsonOptions));
            File.Move(temporaryPath, FilePath, true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Unable to remove custom application '{path}': {exception.Message}");
            return false;
        }
    }

    internal static bool Supports(string path) =>
        File.Exists(path)
        && SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static string? NormalizePath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            return Supports(fullPath) ? fullPath : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsValid(CustomAppDefinition definition) =>
        !string.IsNullOrWhiteSpace(definition.Name) && Supports(definition.LaunchTarget);

    private static AppEntry ToAppEntry(CustomAppDefinition definition) =>
        AppEntry.Application(
            definition.Name,
            definition.LaunchTarget,
            definition.AddedAt,
            isCustom: true);
}