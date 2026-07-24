using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tiles.Layout;

public readonly record struct Win10PinPlacement(TileGroup Group, int Column, int Row);

public static class Win10GroupLayout
{
    public static bool Normalize(TileGroup group)
    {
        var snapshots = group.Tiles.ToDictionary(tile => tile, tile => (tile.Column, tile.Row));
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var tile in group.Tiles)
        {
            var column = tile.Column;
            var row = tile.Row;
            if (!CanFit(group, tile, column, row, occupied))
            {
                if (!TryFindFirstAvailable(group, tile, occupied, out var location))
                {
                    RestorePositions(snapshots);
                    group.RefreshLayout();
                    return false;
                }

                (column, row) = location;
            }

            tile.Column = column;
            tile.Row = row;
            Occupy(tile, column, row, occupied);
        }

        group.RefreshLayout();
        return true;
    }

    public static bool Add(TileGroup target, TileItem tile, int column, int row)
    {
        if (!IsWithinBounds(target, tile, column, row))
        {
            return false;
        }

        var snapshots = CapturePositions(target);
        row = ConstrainDropRow(target, tile, column, row);
        tile.Column = column;
        tile.Row = row;
        target.Tiles.Insert(0, tile);
        if (Normalize(target))
        {
            return true;
        }

        target.Tiles.Remove(tile);
        RestorePositions(snapshots);
        target.RefreshLayout();
        return false;
    }

    public static bool Move(TileGroup source, TileGroup target, TileItem tile, int column, int row)
    {
        if (!source.Tiles.Contains(tile) || !IsWithinBounds(target, tile, column, row))
        {
            return false;
        }

        var sourceSnapshot = CapturePositions(source);
        var targetSnapshot = ReferenceEquals(source, target) ? sourceSnapshot : CapturePositions(target);
        row = ConstrainDropRow(target, tile, column, row);
        source.Tiles.Remove(tile);
        tile.Column = column;
        tile.Row = row;
        target.Tiles.Insert(0, tile);
        if (Normalize(target) && (ReferenceEquals(source, target) || Normalize(source)))
        {
            return true;
        }

        target.Tiles.Remove(tile);
        if (!source.Tiles.Contains(tile))
        {
            source.Tiles.Add(tile);
        }

        RestorePositions(sourceSnapshot);
        if (!ReferenceEquals(source, target))
        {
            RestorePositions(targetSnapshot);
        }

        source.RefreshLayout();
        target.RefreshLayout();
        return false;
    }

    public static bool TryMove(TileGroup group, TileItem tile, int column, int row)
    {
        if (!IsWithinBounds(group, tile, column, row)
            || group.Tiles.Any(other => other != tile && Overlaps(tile, column, row, other)))
        {
            return false;
        }

        tile.Column = column;
        tile.Row = row;
        group.RefreshLayout();
        return true;
    }

    public static bool TryFindFirstAvailable(
        TileGroup group,
        TileItem tile,
        out (int Column, int Row) location)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var other in group.Tiles.Where(other => other != tile))
        {
            Occupy(other, other.Column, other.Row, occupied);
        }

        return TryFindFirstAvailable(group, tile, occupied, out location);
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
            var found = group.ContentRowLimit is null
                ? TryFindAvailableWithinOccupiedRows(group, tile, out var location)
                : TryFindFirstAvailable(group, tile, out location);
            if (found)
            {
                return new Win10PinPlacement(group, location.Column, location.Row);
            }
        }

        return null;
    }

    public static bool AddToFreeCell(TileGroup target, TileItem tile, int column, int row)
    {
        if (!IsWithinBounds(target, tile, column, row)
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

    public static bool CanFitAll(TileGroup group, IEnumerable<TileItem> tiles)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var tile in tiles.OrderByDescending(TileArea).ThenByDescending(tile => tile.Size.ColumnSpan()))
        {
            if (!TryFindFirstAvailable(group, tile, occupied, out var location))
            {
                return false;
            }

            Occupy(tile, location.Column, location.Row, occupied);
        }

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
            return tile.Size.ColumnSpan() <= group.ContentColumns;
        }

        var lastStartRow = occupiedRows - tile.Size.RowSpan();
        for (var row = 0; row <= lastStartRow; row++)
        {
            for (var column = 0; column <= group.ContentColumns - tile.Size.ColumnSpan(); column++)
            {
                if (CanFit(group, tile, column, row, occupied))
                {
                    location = (column, row);
                    return true;
                }
            }
        }

        location = default;
        return false;
    }

    private static bool TryFindFirstAvailable(
        TileGroup group,
        TileItem tile,
        HashSet<(int Column, int Row)> occupied,
        out (int Column, int Row) location)
    {
        var lastStartRow = group.ContentRowLimit is { } rowLimit
            ? rowLimit - tile.Size.RowSpan()
            : int.MaxValue;
        for (var row = 0; row <= lastStartRow; row++)
        {
            for (var column = 0; column <= group.ContentColumns - tile.Size.ColumnSpan(); column++)
            {
                if (CanFit(group, tile, column, row, occupied))
                {
                    location = (column, row);
                    return true;
                }
            }

            if (row == int.MaxValue)
            {
                break;
            }
        }

        location = default;
        return false;
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
        var row = Math.Min(requestedRow, supportedRow);
        return group.ContentRowLimit is { } rowLimit
            ? Math.Min(row, rowLimit - tile.Size.RowSpan())
            : row;
    }

    private static bool IsWithinBounds(TileGroup group, TileItem tile, int column, int row) =>
        column >= 0
        && row >= 0
        && column + tile.Size.ColumnSpan() <= group.ContentColumns
        && (group.ContentRowLimit is null || row + tile.Size.RowSpan() <= group.ContentRowLimit);

    private static bool CanFit(
        TileGroup group,
        TileItem tile,
        int column,
        int row,
        HashSet<(int Column, int Row)> occupied)
    {
        if (!IsWithinBounds(group, tile, column, row))
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

    private static bool Overlaps(TileItem tile, int column, int row, TileItem other) =>
        column < other.Column + other.Size.ColumnSpan()
        && column + tile.Size.ColumnSpan() > other.Column
        && row < other.Row + other.Size.RowSpan()
        && row + tile.Size.RowSpan() > other.Row;

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

    private static Dictionary<TileItem, (int Column, int Row)> CapturePositions(TileGroup group) =>
        group.Tiles.ToDictionary(tile => tile, tile => (tile.Column, tile.Row));

    private static void RestorePositions(IReadOnlyDictionary<TileItem, (int Column, int Row)> snapshots)
    {
        foreach (var (tile, position) in snapshots)
        {
            tile.Column = position.Column;
            tile.Row = position.Row;
        }
    }

    private static int TileArea(TileItem tile) => tile.Size.ColumnSpan() * tile.Size.RowSpan();
}
