namespace TileStart.Host;

public enum TileSize
{
    Small,
    Medium,
    Wide,
    Large,
}

public static class TileSizeExtensions
{
    public static int ColumnSpan(this TileSize size) => size switch
    {
        TileSize.Small => 1,
        TileSize.Medium => 2,
        TileSize.Wide => 4,
        TileSize.Large => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    public static int RowSpan(this TileSize size) => size switch
    {
        TileSize.Small => 1,
        TileSize.Medium => 2,
        TileSize.Wide => 2,
        TileSize.Large => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };
}
