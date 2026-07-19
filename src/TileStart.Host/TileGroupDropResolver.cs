using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace TileStart.Host;

public readonly record struct TileGroupDropTarget(int Column, int Row, Rect Bounds);

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
        foreach (var target in column)
        {
            if (pointer.Y <= CenterY(target.Bounds))
            {
                return new TileGroupCell(target.Column, target.Row);
            }
        }

        return new TileGroupCell(column[0].Column, column.Max(target => target.Row) + 1);
    }

    private static double CenterX(Rect bounds) => bounds.Left + bounds.Width / 2;

    private static double CenterY(Rect bounds) => bounds.Top + bounds.Height / 2;
}