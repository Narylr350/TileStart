namespace TileStart.Host;

public static class TileFolderLayout
{
    public static double ContentHeight(TileItem folder)
    {
        if (folder.FolderTiles.Count == 0)
        {
            return Win10TileMetrics.CellSize;
        }

        var rows = folder.FolderTiles.Max(tile => tile.Row + tile.Size.RowSpan());
        return rows * Win10TileMetrics.CellPitch - Win10TileMetrics.Gap;
    }

    public static double RegionHeight(TileItem folder) =>
        Win10VisualMetrics.TileFolderSeparatorHeight
        + Win10VisualMetrics.TileFolderHeaderHeight
        + ContentHeight(folder)
        + Win10VisualMetrics.TileFolderSeparatorHeight
        + Win10VisualMetrics.TileFolderBottomMargin;

    public static double ToLogicalY(TileGroup group, double displayY)
    {
        var logicalY = displayY;
        foreach (var folder in group.Tiles
                     .Where(tile => tile.IsTileFolder && tile.IsFolderExpanded)
                     .OrderBy(tile => tile.FolderRegionTop))
        {
            if (displayY >= folder.FolderRegionTop + folder.FolderRegionHeight)
            {
                logicalY -= folder.FolderRegionHeight;
            }
        }

        return logicalY;
    }

    public static (int Column, int Row) FindFirstAvailable(TileItem folder, TileItem tile)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var other in folder.FolderTiles.Where(other => other != tile))
        {
            Occupy(other, other.Column, other.Row, occupied);
        }

        return FindFirstAvailable(tile, occupied);
    }

    public static void Normalize(TileItem folder)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var tile in folder.FolderTiles)
        {
            var column = tile.Column;
            var row = tile.Row;
            if (!CanFit(tile, column, row, occupied))
            {
                (column, row) = FindFirstAvailable(tile, occupied);
                tile.Column = column;
                tile.Row = row;
            }

            Occupy(tile, column, row, occupied);
        }
    }

    public static bool AddOrMove(TileItem folder, TileItem tile, int column, int row)
    {
        if (tile.IsTileFolder
            || column < 0
            || row < 0
            || column + tile.Size.ColumnSpan() > Win10TileMetrics.GroupColumns)
        {
            return false;
        }

        folder.FolderTiles.Remove(tile);
        tile.Column = column;
        tile.Row = row;
        folder.FolderTiles.Insert(0, tile);
        Normalize(folder);
        return true;
    }

    private static (int Column, int Row) FindFirstAvailable(
        TileItem tile,
        HashSet<(int Column, int Row)> occupied)
    {
        for (var row = 0; ; row++)
        {
            for (var column = 0; column <= Win10TileMetrics.GroupColumns - tile.Size.ColumnSpan(); column++)
            {
                if (CanFit(tile, column, row, occupied))
                {
                    return (column, row);
                }
            }
        }
    }

    private static bool CanFit(
        TileItem tile,
        int column,
        int row,
        HashSet<(int Column, int Row)> occupied)
    {
        if (column < 0 || row < 0 || column + tile.Size.ColumnSpan() > Win10TileMetrics.GroupColumns)
        {
            return false;
        }

        for (var y = row; y < row + tile.Size.RowSpan(); y++)
        {
            for (var x = column; x < column + tile.Size.ColumnSpan(); x++)
            {
                if (occupied.Contains((x, y)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Occupy(
        TileItem tile,
        int column,
        int row,
        HashSet<(int Column, int Row)> occupied)
    {
        for (var y = row; y < row + tile.Size.RowSpan(); y++)
        {
            for (var x = column; x < column + tile.Size.ColumnSpan(); x++)
            {
                occupied.Add((x, y));
            }
        }
    }
}
