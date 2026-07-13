using System.IO;
using System.Text.Json;

namespace TileStart.Host;

public static class WindowSizeStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");
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
                : (size.Width, size.Height);
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new SavedSize(width, height)));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record SavedSize(double Width, double Height);
}
