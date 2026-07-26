using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TileStart.Host.Utilities;

namespace TileStart.Host.Themes;

public static class AppearancePreferencesStore
{
    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "appearance.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppearancePreferences Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? Deserialize(File.ReadAllText(FilePath))
                : new AppearancePreferences();
        }
        catch (IOException exception)
        {
            DiagnosticLog.Write($"Unable to load appearance preferences: {exception.Message}");
            return new AppearancePreferences();
        }
        catch (JsonException exception)
        {
            DiagnosticLog.Write($"Unable to parse appearance preferences: {exception.Message}");
            return new AppearancePreferences();
        }
        catch (UnauthorizedAccessException exception)
        {
            DiagnosticLog.Write($"Unable to read appearance preferences: {exception.Message}");
            return new AppearancePreferences();
        }
    }

    public static void Save(AppearancePreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, Serialize(preferences));
            File.Move(temporaryPath, FilePath, true);
        }
        catch (IOException exception)
        {
            DiagnosticLog.Write($"Unable to save appearance preferences: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            DiagnosticLog.Write($"Unable to write appearance preferences: {exception.Message}");
        }
    }

    internal static string Serialize(AppearancePreferences preferences) =>
        JsonSerializer.Serialize(preferences, JsonOptions);

    internal static AppearancePreferences Deserialize(string json) =>
        JsonSerializer.Deserialize<AppearancePreferences>(json, JsonOptions) ?? new AppearancePreferences();
}
