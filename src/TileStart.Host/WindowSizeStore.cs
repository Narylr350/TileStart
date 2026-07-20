using System.IO;
using System.Text.Json;

namespace TileStart.Host;

public static class WindowSizeStore
{
    internal const int CurrentFormatVersion = 1;

    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "window.json");

    public static (double Width, double Height)? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var size = JsonSerializer.Deserialize<SavedSize>(File.ReadAllText(FilePath));
            return size is null || !double.IsFinite(size.Width) || !double.IsFinite(size.Height)
                ? null
                : (MigrateWidth(size.Width, size.Version), size.Height);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(double width, double height)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new SavedSize
            {
                Width = width,
                Height = height,
                Version = CurrentFormatVersion,
            }));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    internal static double MigrateWidth(double width, int version) =>
        version < CurrentFormatVersion
            ? width + Win10VisualMetrics.TileScrollBarLayoutWidth
            : width;

    private sealed class SavedSize
    {
        public double Width { get; init; }
        public double Height { get; init; }
        public int Version { get; init; }
    }
}
