using System.IO;
using System.Text.Json;

namespace TileStart.Host;

public static class NavigationPreferencesStore
{
    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "navigation.json");

    public static NavigationPreferences Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? Deserialize(File.ReadAllText(FilePath))
                : new NavigationPreferences();
        }
        catch (IOException exception)
        {
            DiagnosticLog.Write($"Unable to load navigation preferences: {exception.Message}");
            return new NavigationPreferences();
        }
        catch (JsonException exception)
        {
            DiagnosticLog.Write($"Unable to parse navigation preferences: {exception.Message}");
            return new NavigationPreferences();
        }
        catch (UnauthorizedAccessException exception)
        {
            DiagnosticLog.Write($"Unable to read navigation preferences: {exception.Message}");
            return new NavigationPreferences();
        }
    }

    public static void Save(NavigationPreferences preferences)
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
            DiagnosticLog.Write($"Unable to save navigation preferences: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            DiagnosticLog.Write($"Unable to write navigation preferences: {exception.Message}");
        }
    }

    internal static string Serialize(NavigationPreferences preferences) =>
        JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });

    internal static NavigationPreferences Deserialize(string json) =>
        JsonSerializer.Deserialize<NavigationPreferences>(json) ?? new NavigationPreferences();
}
