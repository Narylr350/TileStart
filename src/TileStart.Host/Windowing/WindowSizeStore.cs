using System.IO;
using System.Text.Json;

namespace TileStart.Host.Windowing;

public readonly record struct WindowSizePreference(int WorkspaceColumns, double Height);

public static class WindowSizeStore
{
    internal const int CurrentFormatVersion = 2;

    private static readonly string DirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileStart");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "window.json");

    public static WindowSizePreference? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var size = JsonSerializer.Deserialize<SavedSize>(File.ReadAllText(FilePath));
            if (size is null
                || !double.IsFinite(size.Height)
                || size.Height <= 0
                || size.Version < CurrentFormatVersion && (!double.IsFinite(size.Width) || size.Width <= 0))
            {
                return null;
            }

            var columns = size.Version >= CurrentFormatVersion
                ? size.WorkspaceColumns
                : StartWindowSizing.ColumnsForWidth(MigrateWidth(size.Width, size.Version));
            return columns is < StartWindowSizing.MinimumGroupColumns or > StartWindowSizing.MaximumGroupColumns
                ? null
                : new WindowSizePreference(columns, size.Height);
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

    public static void Save(int workspaceColumns, double height)
    {
        if (workspaceColumns is < StartWindowSizing.MinimumGroupColumns or > StartWindowSizing.MaximumGroupColumns
            || !double.IsFinite(height)
            || height <= 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new SavedSize
            {
                WorkspaceColumns = workspaceColumns,
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
        version < 1
            ? width + Win10VisualMetrics.TileScrollBarLayoutWidth
            : width;

    private sealed class SavedSize
    {
        public double Width { get; init; }
        public double Height { get; init; }
        public int WorkspaceColumns { get; init; }
        public int Version { get; init; }
    }
}