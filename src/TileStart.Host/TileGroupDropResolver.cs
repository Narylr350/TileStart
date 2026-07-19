using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace TileStart.Host;

public readonly record struct TileGroupDropTarget(int Index, Rect Bounds);

public static class TileGroupDropResolver
{
    private const double ColumnTolerance = 0.5;

    public static int ResolveTargetIndex(Point pointer, IReadOnlyList<TileGroupDropTarget> targets)
    {
        if (targets.Count == 0)
        {
            return 0;
        }

        var nearestColumnCenter = targets
            .OrderBy(target => Math.Abs(CenterX(target.Bounds) - pointer.X))
            .First()
            .Bounds;
        var columnCenter = CenterX(nearestColumnCenter);
        var column = targets
            .Where(target => Math.Abs(CenterX(target.Bounds) - columnCenter) <= ColumnTolerance)
            .OrderBy(target => target.Bounds.Top)
            .ToArray();

        // The native resolver returns a two-dimensional cell. The current packed panel has no
        // persisted group cells yet, so its frozen visual slots are the compatibility mapping.
        return column
            .OrderBy(target => Math.Abs(CenterY(target.Bounds) - pointer.Y))
            .First()
            .Index;
    }

    private static double CenterX(Rect bounds) => bounds.Left + bounds.Width / 2;

    private static double CenterY(Rect bounds) => bounds.Top + bounds.Height / 2;
}