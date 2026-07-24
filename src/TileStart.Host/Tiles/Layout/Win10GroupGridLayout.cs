using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tiles.Layout;

public readonly record struct TileGroupCell(int Column, int Row);

public static class Win10GroupGridLayout
{
    public static bool EnsureCoordinates(TileLayout layout, int columns)
    {
        columns = NormalizeColumns(layout.Groups, columns);
        var changed = false;
        var occupied = new Dictionary<int, HashSet<int>>();
        foreach (var group in layout.Groups)
        {
            var requested = GetCell(group);
            var cell = IsAvailable(requested, group.WidthUnits, columns, occupied)
                ? requested
                : FindFirstAvailable(group.WidthUnits, columns, occupied);
            if (requested != cell)
            {
                SetCell(group, cell);
                changed = true;
            }

            Occupy(cell, group.WidthUnits, occupied);
        }

        return changed;
    }

    public static TileGroupCell FindAppendCell(TileLayout layout, int columns)
    {
        var probe = new TileGroup();
        return FindAppendCell(layout, probe, columns);
    }

    public static TileGroupCell FindAppendCell(TileLayout layout, TileGroup group, int columns)
    {
        columns = NormalizeColumns(layout.Groups.Append(group), columns);
        EnsureCoordinates(layout, columns);
        var occupied = BuildOccupied(layout.Groups.Where(candidate => !ReferenceEquals(candidate, group)));
        return FindFirstAvailable(group.WidthUnits, columns, occupied);
    }

    public static void Insert(TileLayout layout, TileGroup group, TileGroupCell target, int columns)
    {
        if (!layout.Groups.Contains(group))
        {
            throw new ArgumentException("The inserted group must belong to the layout.", nameof(group));
        }

        columns = NormalizeColumns(layout.Groups, columns);
        var others = layout.Groups.Where(candidate => !ReferenceEquals(candidate, group)).ToArray();
        EnsureCoordinates(new TileLayout { Groups = [.. others] }, columns);
        var occupied = BuildOccupied(others);
        var cell = FindNearestAvailable(target, group.WidthUnits, columns, occupied);
        SetCell(group, cell);
    }

    public static bool Move(TileLayout layout, TileGroup group, TileGroupCell target, int columns)
    {
        if (!layout.Groups.Contains(group))
        {
            return false;
        }

        columns = NormalizeColumns(layout.Groups, columns);
        EnsureCoordinates(layout, columns);
        var source = GetCell(group);
        var clampedTarget = new TileGroupCell(
            Math.Clamp(target.Column, 0, columns - Math.Min(group.WidthUnits, columns)),
            Math.Max(0, target.Row));
        if (source == clampedTarget)
        {
            return false;
        }

        var others = layout.Groups.Where(candidate => !ReferenceEquals(candidate, group)).ToArray();
        var occupied = BuildOccupied(others);
        if (IsAvailable(clampedTarget, group.WidthUnits, columns, occupied))
        {
            SetCell(group, clampedTarget);
            return true;
        }

        var occupant = others.FirstOrDefault(candidate => Overlaps(
            clampedTarget,
            group.WidthUnits,
            GetCell(candidate),
            candidate.WidthUnits));
        if (occupant is not null && occupant.WidthUnits == group.WidthUnits)
        {
            var withoutOccupant = BuildOccupied(others.Where(candidate => !ReferenceEquals(candidate, occupant)));
            if (IsAvailable(source, occupant.WidthUnits, columns, withoutOccupant))
            {
                SetCell(occupant, source);
                SetCell(group, clampedTarget);
                return true;
            }
        }

        var destination = FindNearestAvailable(clampedTarget, group.WidthUnits, columns, occupied);
        if (destination == source)
        {
            return false;
        }

        SetCell(group, destination);
        return true;
    }

    public static bool Remove(TileLayout layout, TileGroup group) => layout.Groups.Remove(group);

    public static TileGroupCell GetCell(TileGroup group) => new(group.GroupColumn, group.GroupRow);

    public static void SetCell(TileGroup group, TileGroupCell cell)
    {
        group.GroupColumn = cell.Column;
        group.GroupRow = cell.Row;
    }

    private static int NormalizeColumns(IEnumerable<TileGroup> groups, int columns) =>
        Math.Max(Math.Max(1, columns), groups.Select(group => group.WidthUnits).DefaultIfEmpty(1).Max());

    private static Dictionary<int, HashSet<int>> BuildOccupied(IEnumerable<TileGroup> groups)
    {
        var occupied = new Dictionary<int, HashSet<int>>();
        foreach (var group in groups)
        {
            Occupy(GetCell(group), group.WidthUnits, occupied);
        }

        return occupied;
    }

    private static bool IsAvailable(
        TileGroupCell cell,
        int span,
        int columns,
        IReadOnlyDictionary<int, HashSet<int>> occupied)
    {
        span = Math.Clamp(span, 1, columns);
        if (cell.Column < 0 || cell.Row < 0 || cell.Column + span > columns)
        {
            return false;
        }

        return !occupied.TryGetValue(cell.Row, out var row)
               || Enumerable.Range(cell.Column, span).All(column => !row.Contains(column));
    }

    private static TileGroupCell FindFirstAvailable(
        int span,
        int columns,
        IReadOnlyDictionary<int, HashSet<int>> occupied)
    {
        span = Math.Clamp(span, 1, columns);
        for (var row = 0;; row++)
        {
            for (var column = 0; column <= columns - span; column++)
            {
                var cell = new TileGroupCell(column, row);
                if (IsAvailable(cell, span, columns, occupied))
                {
                    return cell;
                }
            }
        }
    }

    private static TileGroupCell FindNearestAvailable(
        TileGroupCell target,
        int span,
        int columns,
        IReadOnlyDictionary<int, HashSet<int>> occupied)
    {
        span = Math.Clamp(span, 1, columns);
        var maxOccupiedRow = occupied.Keys.DefaultIfEmpty(0).Max();
        var maxDistance = Math.Max(columns, maxOccupiedRow + 2);
        for (var distance = 0; distance <= maxDistance; distance++)
        {
            var candidates = new List<TileGroupCell>();
            for (var rowOffset = -distance; rowOffset <= distance; rowOffset++)
            {
                var columnOffset = distance - Math.Abs(rowOffset);
                candidates.Add(new TileGroupCell(target.Column - columnOffset, target.Row + rowOffset));
                if (columnOffset != 0)
                {
                    candidates.Add(new TileGroupCell(target.Column + columnOffset, target.Row + rowOffset));
                }
            }

            foreach (var candidate in candidates
                         .Where(candidate => candidate.Row >= 0)
                         .OrderBy(candidate => Math.Abs(candidate.Row - target.Row))
                         .ThenBy(candidate => Math.Abs(candidate.Column - target.Column))
                         .ThenBy(candidate => candidate.Row)
                         .ThenBy(candidate => candidate.Column))
            {
                if (IsAvailable(candidate, span, columns, occupied))
                {
                    return candidate;
                }
            }
        }

        return FindFirstAvailable(span, columns, occupied);
    }

    private static void Occupy(
        TileGroupCell cell,
        int span,
        IDictionary<int, HashSet<int>> occupied)
    {
        if (cell.Row < 0 || cell.Column < 0)
        {
            return;
        }

        if (!occupied.TryGetValue(cell.Row, out var row))
        {
            row = [];
            occupied.Add(cell.Row, row);
        }

        for (var column = cell.Column; column < cell.Column + span; column++)
        {
            row.Add(column);
        }
    }

    private static bool Overlaps(
        TileGroupCell first,
        int firstSpan,
        TileGroupCell second,
        int secondSpan) =>
        first.Row == second.Row
        && first.Column < second.Column + secondSpan
        && first.Column + firstSpan > second.Column;
}
