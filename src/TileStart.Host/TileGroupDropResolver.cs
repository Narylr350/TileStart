using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace TileStart.Host;

public readonly record struct TileGroupDropTarget(int Column, int Row, Rect Bounds, bool IsEmptyColumn = false);

public static class TileGroupDropResolver
{
    private const double ColumnTolerance = 0.5;

    public static TileGroupCell ResolveTargetCell(
        Point pointer,
        IReadOnlyList<TileGroupDropTarget> targets)
    {
        if (targets.Count == 0)
        {
            return new TileGroupCell(0, 0);
        }

        var nearestColumnCenter = targets
            .OrderBy(target => Math.Abs(CenterX(target.Bounds) - pointer.X))
            .First();
        var column = targets
            .Where(target => Math.Abs(CenterX(target.Bounds) - CenterX(nearestColumnCenter.Bounds)) <= ColumnTolerance)
            .OrderBy(target => target.Row)
            .ToArray();
        if (column[0].IsEmptyColumn)
        {
            return new TileGroupCell(column[0].Column, 0);
        }

        foreach (var target in column)
        {
            if (pointer.Y <= CenterY(target.Bounds))
            {
                return new TileGroupCell(target.Column, target.Row);
            }
        }

        return new TileGroupCell(column[0].Column, column.Max(target => target.Row) + 1);
    }

    public static TileGroupDropTarget[] IncludeEmptyColumns(
        IEnumerable<TileGroupDropTarget> source,
        int columns)
    {
        columns = Math.Max(1, columns);
        var targets = source.ToList();
        var originLeft = targets.Count == 0
            ? 0
            : targets.Min(target => target.Bounds.Left - target.Column * Win10TileMetrics.GroupPitch);
        for (var column = 0; column < columns; column++)
        {
            if (targets.Any(target => target.Column == column))
            {
                continue;
            }

            targets.Add(new TileGroupDropTarget(
                column,
                0,
                new Rect(
                    originLeft + column * Win10TileMetrics.GroupPitch,
                    0,
                    Win10TileMetrics.GroupWidth,
                    0),
                IsEmptyColumn: true));
        }

        return [.. targets];
    }

    private static double CenterX(Rect bounds) => bounds.Left + bounds.Width / 2;

    private static double CenterY(Rect bounds) => bounds.Top + bounds.Height / 2;
}
