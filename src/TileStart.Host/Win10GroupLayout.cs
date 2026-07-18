namespace TileStart.Host;

public static class Win10GroupLayout
{
    public static void Normalize(TileGroup group)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var tile in group.Tiles)
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

        group.RefreshLayout();
    }

    public static bool Add(TileGroup target, TileItem tile, int column, int row)
    {
        if (column < 0 || row < 0 || column + tile.Size.ColumnSpan() > Win10TileMetrics.GroupColumns)
        {
            return false;
        }

        row = ConstrainDropRow(target, tile, column, row);
        tile.Column = column;
        tile.Row = row;
        target.Tiles.Insert(0, tile);
        Normalize(target);
        return true;
    }

    public static bool Move(TileGroup source, TileGroup target, TileItem tile, int column, int row)
    {
        if (!source.Tiles.Contains(tile)
            || column < 0
            || row < 0
            || column + tile.Size.ColumnSpan() > Win10TileMetrics.GroupColumns)
        {
            return false;
        }

        row = ConstrainDropRow(target, tile, column, row);
        source.Tiles.Remove(tile);
        tile.Column = column;
        tile.Row = row;
        target.Tiles.Insert(0, tile);
        Normalize(target);
        if (source != target)
        {
            Normalize(source);
        }

        return true;
    }

    public static bool TryMove(TileGroup group, TileItem tile, int column, int row)
    {
        if (column < 0 || row < 0 || column + tile.Size.ColumnSpan() > Win10TileMetrics.GroupColumns)
        {
            return false;
        }

        if (group.Tiles.Any(other => other != tile && Overlaps(tile, column, row, other)))
        {
            return false;
        }

        tile.Column = column;
        tile.Row = row;
        group.RefreshLayout();
        return true;
    }

    public static (int Column, int Row) FindFirstAvailable(TileGroup group, TileItem tile)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var other in group.Tiles.Where(other => other != tile))
        {
            Occupy(other, other.Column, other.Row, occupied);
        }

        return FindFirstAvailable(tile, occupied);
    }

    private static (int Column, int Row) FindFirstAvailable(TileItem tile, HashSet<(int Column, int Row)> occupied)
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

    private static int ConstrainDropRow(TileGroup group, TileItem tile, int column, int requestedRow)
    {
        var right = column + tile.Size.ColumnSpan();
        var supportedRow = group.Tiles
            .Where(other => !ReferenceEquals(other, tile)
                            && column < other.Column + other.Size.ColumnSpan()
                            && right > other.Column)
            .Select(other => other.Row + other.Size.RowSpan())
            .DefaultIfEmpty(0)
            .Max();
        return Math.Min(requestedRow, supportedRow);
    }

    private static bool CanFit(TileItem tile, int column, int row, HashSet<(int Column, int Row)> occupied)
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

    private static bool Overlaps(TileItem tile, int column, int row, TileItem other)
    {
        return column < other.Column + other.Size.ColumnSpan()
            && column + tile.Size.ColumnSpan() > other.Column
            && row < other.Row + other.Size.RowSpan()
            && row + tile.Size.RowSpan() > other.Row;
    }

    private static void Occupy(TileItem tile, int column, int row, HashSet<(int Column, int Row)> occupied)
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
