namespace TileStart.Host;

public readonly record struct TileGroupCell(int Column, int Row);

public static class Win10GroupGridLayout
{
    public static bool EnsureCoordinates(TileLayout layout, int columns)
    {
        return EnsureCoordinates(layout.Groups, columns);
    }

    private static bool EnsureCoordinates(IEnumerable<TileGroup> source, int columns)
    {
        columns = Math.Max(1, columns);
        var groups = source.ToArray();
        var occupied = new HashSet<TileGroupCell>();
        var coordinatesAreValid = groups.All(group =>
            group.GroupColumn >= 0
            && group.GroupColumn < columns
            && group.GroupRow >= 0
            && occupied.Add(new TileGroupCell(group.GroupColumn, group.GroupRow)));
        if (!coordinatesAreValid)
        {
            for (var index = 0; index < groups.Length; index++)
            {
                SetCell(groups[index], new TileGroupCell(index % columns, index / columns));
            }

            return groups.Length > 0;
        }

        var changed = false;
        foreach (var column in groups.GroupBy(group => group.GroupColumn))
        {
            var row = 0;
            foreach (var group in column.OrderBy(group => group.GroupRow))
            {
                if (group.GroupRow != row)
                {
                    group.GroupRow = row;
                    changed = true;
                }

                row++;
            }
        }

        return changed;
    }

    public static TileGroupCell FindAppendCell(TileLayout layout, int columns)
    {
        EnsureCoordinates(layout, columns);
        return Enumerable.Range(0, Math.Max(1, columns))
            .Select(column => new TileGroupCell(
                column,
                layout.Groups.Count(group => group.GroupColumn == column)))
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .First();
    }

    public static void Insert(TileLayout layout, TileGroup group, TileGroupCell target, int columns)
    {
        if (!layout.Groups.Contains(group))
        {
            throw new ArgumentException("The inserted group must belong to the layout.", nameof(group));
        }

        EnsureCoordinates(layout.Groups.Where(candidate => candidate != group), columns);
        var column = Math.Clamp(target.Column, 0, Math.Max(1, columns) - 1);
        var row = Math.Clamp(
            target.Row,
            0,
            layout.Groups.Count(candidate => candidate != group && candidate.GroupColumn == column));
        foreach (var candidate in layout.Groups.Where(candidate =>
                     candidate != group
                     && candidate.GroupColumn == column
                     && candidate.GroupRow >= row))
        {
            candidate.GroupRow++;
        }

        SetCell(group, new TileGroupCell(column, row));
    }

    public static bool Move(TileLayout layout, TileGroup group, TileGroupCell target, int columns)
    {
        if (!layout.Groups.Contains(group))
        {
            return false;
        }

        EnsureCoordinates(layout, columns);
        var source = GetCell(group);
        var column = Math.Clamp(target.Column, 0, Math.Max(1, columns) - 1);
        var maxRow = layout.Groups.Count(candidate => candidate != group && candidate.GroupColumn == column);
        var destination = new TileGroupCell(column, Math.Clamp(target.Row, 0, maxRow));
        if (source == destination)
        {
            return false;
        }

        var occupant = layout.Groups.FirstOrDefault(candidate =>
            candidate != group
            && candidate.GroupColumn == destination.Column
            && candidate.GroupRow == destination.Row);
        if (occupant is not null)
        {
            SetCell(occupant, source);
            SetCell(group, destination);
            return true;
        }

        SetCell(group, destination);
        CompactColumn(layout, source.Column, group);
        return true;
    }

    public static bool Remove(TileLayout layout, TileGroup group)
    {
        if (!layout.Groups.Remove(group))
        {
            return false;
        }

        CompactColumn(layout, group.GroupColumn);
        return true;
    }

    public static TileGroupCell GetCell(TileGroup group) => new(group.GroupColumn, group.GroupRow);

    public static void SetCell(TileGroup group, TileGroupCell cell)
    {
        group.GroupColumn = cell.Column;
        group.GroupRow = cell.Row;
    }

    private static void CompactColumn(TileLayout layout, int column, TileGroup? excluded = null)
    {
        var row = 0;
        foreach (var group in layout.Groups
                     .Where(group => group != excluded && group.GroupColumn == column)
                     .OrderBy(group => group.GroupRow))
        {
            group.GroupRow = row++;
        }
    }
}