namespace TileStart.Host;

public static class Win10TileMetrics
{
    public const int GroupColumns = 8;
    public const double CellSize = 48;
    public const double Gap = 4;
    public const double CellPitch = CellSize + Gap;
    public const double GroupWidth = GroupColumns * CellPitch - Gap;
    public const double GroupGap = 16;
    public const double GroupPitch = GroupWidth + GroupGap;

    public static double Width(TileSize size) => size.ColumnSpan() * CellPitch - Gap;

    public static double Height(TileSize size) => size.RowSpan() * CellPitch - Gap;

    public static double MaxIconSize(TileSize size) => Math.Min(Width(size), Height(size));

    public static double ScaleIconSize(double iconSize, TileSize previousSize, TileSize nextSize)
    {
        var previousLimit = MaxIconSize(previousSize);
        var nextLimit = MaxIconSize(nextSize);
        var scaled = iconSize * nextLimit / previousLimit;
        return Math.Clamp(Math.Round(scaled), 16, nextLimit);
    }

    public static double Left(int column) => column * CellPitch;

    public static double Top(int row) => row * CellPitch;

    public static int GroupsPerRow(double availableWidth) =>
        Math.Max(1, (int)(availableWidth / GroupPitch));

    public static Win10TileBounds Bounds(TileSize size, int column, int row) =>
        new(Left(column), Top(row), Width(size), Height(size));
}

public readonly record struct Win10TileBounds(double Left, double Top, double Width, double Height);
