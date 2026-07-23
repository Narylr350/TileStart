using System.IO;
using System.Text.Json;

namespace TileStart.Host;

internal static class AppVisibilityStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TileStart",
        "hidden-apps.json");

    public static HashSet<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                JsonSerializer.Deserialize<string[]>(File.ReadAllText(FilePath)) ?? [],
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Unable to load hidden applications: {exception.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Hide(string identity)
    {
        var identities = Load();
        if (identities.Add(identity))
        {
            Save(identities);
        }
    }

    public static void Show(string identity)
    {
        var identities = Load();
        if (identities.Remove(identity))
        {
            Save(identities);
        }
    }

    private static void Save(IEnumerable<string> identities)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(identities.Order(), new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
            File.Move(temporaryPath, FilePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write($"Unable to save hidden applications: {exception.Message}");
        }
    }
}
