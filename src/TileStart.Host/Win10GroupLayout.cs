namespace TileStart.Host;

public readonly record struct Win10PinPlacement(TileGroup Group, int Column, int Row);

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

    public static Win10PinPlacement? FindPinPlacement(IEnumerable<TileGroup> groups, TileItem tile)
    {
        var orderedGroups = groups
            .Select((group, index) => new { Group = group, Index = index })
            .OrderBy(item => item.Group.GroupRow < 0 ? int.MaxValue : item.Group.GroupRow)
            .ThenBy(item => item.Group.GroupColumn < 0 ? int.MaxValue : item.Group.GroupColumn)
            .ThenBy(item => item.Index)
            .Select(item => item.Group)
            .ToArray();
        foreach (var group in orderedGroups)
        {
            if (TryFindAvailableWithinOccupiedRows(group, tile, out var location))
            {
                return new Win10PinPlacement(group, location.Column, location.Row);
            }
        }

        return null;
    }

    public static bool AddToFreeCell(TileGroup target, TileItem tile, int column, int row)
    {
        if (column < 0
            || row < 0
            || column + tile.Size.ColumnSpan() > Win10TileMetrics.GroupColumns
            || target.Tiles.Any(other => other != tile && Overlaps(tile, column, row, other)))
        {
            return false;
        }

        tile.Column = column;
        tile.Row = row;
        target.Tiles.Add(tile);
        target.RefreshLayout();
        return true;
    }

    private static bool TryFindAvailableWithinOccupiedRows(
        TileGroup group,
        TileItem tile,
        out (int Column, int Row) location)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        var occupiedRows = 0;
        foreach (var other in group.Tiles.Where(other => other != tile))
        {
            Occupy(other, other.Column, other.Row, occupied);
            occupiedRows = Math.Max(occupiedRows, other.Row + other.Size.RowSpan());
        }

        if (occupiedRows == 0)
        {
            location = (0, 0);
            return true;
        }

        var lastStartRow = occupiedRows - tile.Size.RowSpan();
        for (var row = 0; row <= lastStartRow; row++)
        {
            for (var column = 0; column <= Win10TileMetrics.GroupColumns - tile.Size.ColumnSpan(); column++)
            {
                if (CanFit(tile, column, row, occupied))
                {
                    location = (column, row);
                    return true;
                }
            }
        }

        location = default;
        return false;
    }

    private static (int Column, int Row) FindFirstAvailable(TileItem tile, HashSet<(int Column, int Row)> occupied)
    {
        for (var row = 0;; row++)
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