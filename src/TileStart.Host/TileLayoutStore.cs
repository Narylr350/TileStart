using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TileStart.Host;

public static class TileLayoutStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "layout.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TileLayout? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var layout = Deserialize(File.ReadAllText(FilePath));
            if (layout is null)
            {
                return null;
            }

            return layout;
        }
        catch (IOException exception)
        {
            DiagnosticLog.Write($"Unable to load tile layout: {exception.Message}");
            return null;
        }
        catch (JsonException exception)
        {
            DiagnosticLog.Write($"Unable to parse tile layout: {exception.Message}");
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            DiagnosticLog.Write($"Unable to read tile layout: {exception.Message}");
            return null;
        }
    }

    internal static string Serialize(TileLayout layout)
    {
        return JsonSerializer.Serialize(layout, JsonOptions);
    }

    internal static TileLayout? Deserialize(string json)
    {
        var layout = JsonSerializer.Deserialize<TileLayout>(json, JsonOptions);
        if (layout is null)
        {
            return null;
        }

        foreach (var group in layout.Groups)
        {
            Win10GroupLayout.Normalize(group);
        }

        return layout;
    }

    public static void Save(TileLayout layout)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, Serialize(layout));
            File.Move(temporaryPath, FilePath, true);
        }
        catch (IOException exception)
        {
            DiagnosticLog.Write($"Unable to save tile layout: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            DiagnosticLog.Write($"Unable to write tile layout: {exception.Message}");
        }
    }
}
