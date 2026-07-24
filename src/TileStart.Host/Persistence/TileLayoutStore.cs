using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TileStart.Host.Tiles.Layout;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Utilities;

namespace TileStart.Host.Persistence;

public static class TileLayoutStore
{
    internal const int CurrentVersion = 2;

    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");

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
        layout.Version = CurrentVersion;
        return JsonSerializer.Serialize(layout, JsonOptions);
    }

    internal static TileLayout? Deserialize(string json)
    {
        var layout = JsonSerializer.Deserialize<TileLayout>(json, JsonOptions);
        if (layout is null)
        {
            return null;
        }

        if (layout.Version < CurrentVersion)
        {
            // Version 1 stored GroupColumn in whole-group units. The first custom-grid
            // prototype could also persist partially converted values without a version.
            // Resetting outer cells lets the runtime repack the saved group order using
            // the actual DIP-derived workspace width without touching tile contents.
            foreach (var group in layout.Groups)
            {
                group.GroupColumn = -1;
                group.GroupRow = -1;
            }

            layout.Version = CurrentVersion;
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