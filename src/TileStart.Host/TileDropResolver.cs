namespace TileStart.Host;

public static class TileDropResolver
{
    public const int ReflowDelayMilliseconds = 120;

    public static (int Column, int Row) GetCell(System.Windows.Point pointer, System.Windows.Point anchor, TileItem tile)
    {
        var left = pointer.X - anchor.X;
        var top = pointer.Y - anchor.Y;
        return (
            Math.Clamp((int)Math.Round(left / Win10TileMetrics.CellPitch),
                       0,
                       Win10TileMetrics.GroupColumns - tile.Size.ColumnSpan()),
            Math.Max(0, (int)Math.Round(top / Win10TileMetrics.CellPitch)));
    }

    public static TileItem? FindFolderTarget(
        TileGroup group,
        TileItem moving,
        System.Windows.Point pointer,
        System.Windows.Point anchor)
    {
        if (moving.IsTileFolder)
        {
            return null;
        }

        var left = pointer.X - anchor.X;
        var top = pointer.Y - anchor.Y;
        var centerX = left + moving.PixelWidth / 2;
        var centerY = top + moving.PixelHeight / 2;

        return group.Tiles.FirstOrDefault(tile =>
            !ReferenceEquals(tile, moving)
            && centerX >= tile.Left
            && centerX < tile.Left + tile.PixelWidth
            && centerY >= tile.DisplayTop
            && centerY < tile.DisplayTop + tile.PixelHeight);
    }
}
