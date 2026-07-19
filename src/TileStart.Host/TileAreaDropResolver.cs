namespace TileStart.Host;

public readonly record struct TileGroupDropZone(
    string GroupId,
    double Left,
    double Top,
    double Width,
    double Height,
    double DetachmentHeight = double.NaN,
    int GroupColumn = -1,
    int GroupRow = -1);

public readonly record struct TileNewGroupDropTarget(
    int GroupColumn,
    int GroupRow,
    int TileColumn,
    int TileRow);

public static class TileAreaDropResolver
{
    // Native Start uses RearrangeNewContainerLocation plus private GridLayoutMetrics.
    // Until TileStart persists outer group cells, one cell pitch is the conservative detach band.
    public const double NewGroupDetachmentDistance = Win10TileMetrics.CellPitch;
    public const double NewGroupCreationBand = Win10VisualMetrics.TileGroupHeaderHeight / 2;

    public static TileGroupDropZone? FindTarget(
        IEnumerable<TileGroupDropZone> zones,
        double pointerX,
        double pointerY)
    {
        return FindTarget(zones, pointerX, pointerY, NewGroupDetachmentDistance, useDetachmentHeight: false);
    }

    public static TileGroupDropZone? FindTargetForDraggedTile(
        IEnumerable<TileGroupDropZone> zones,
        double draggedLeft,
        double draggedTop,
        double draggedWidth,
        double draggedHeight)
    {
        return FindTarget(
            zones,
            draggedLeft + draggedWidth / 2,
            draggedTop + draggedHeight / 2,
            NewGroupCreationBand,
            useDetachmentHeight: true);
    }

    public static TileNewGroupDropTarget FindNewGroupTargetForDraggedTile(
        IEnumerable<TileGroupDropZone> zones,
        double draggedLeft,
        double draggedTop,
        double draggedHeight,
        int columnSpan,
        int groupColumns)
    {
        groupColumns = Math.Max(1, groupColumns);
        var candidates = zones.Where(zone => zone.GroupColumn >= 0 && zone.GroupRow >= 0).ToArray();
        if (candidates.Length == 0)
        {
            return new TileNewGroupDropTarget(
                0,
                0,
                ClampColumn(draggedLeft, 0, columnSpan),
                0);
        }

        var centerX = draggedLeft + (columnSpan * Win10TileMetrics.CellPitch - Win10TileMetrics.Gap) / 2;
        var centerY = draggedTop + draggedHeight / 2;
        var originLeft = candidates.Min(zone => zone.Left - zone.GroupColumn * Win10TileMetrics.GroupPitch);
        var groupColumn = Math.Clamp(
            (int)Math.Round(
                (centerX - originLeft - Win10TileMetrics.GroupWidth / 2)
                / Win10TileMetrics.GroupPitch),
            0,
            groupColumns - 1);
        var groupLeft = originLeft + groupColumn * Win10TileMetrics.GroupPitch;
        var column = candidates
            .Where(zone => zone.GroupColumn == groupColumn)
            .OrderBy(zone => zone.GroupRow)
            .ToArray();
        var followingIndex = Array.FindIndex(column, zone => zone.Top > centerY);
        var groupRow = followingIndex >= 0
            ? column[followingIndex].GroupRow
            : column.Length;
        return new TileNewGroupDropTarget(
            groupColumn,
            groupRow,
            ClampColumn(draggedLeft, groupLeft, columnSpan),
            0);
    }

    private static TileGroupDropZone? FindTarget(
        IEnumerable<TileGroupDropZone> zones,
        double targetX,
        double targetY,
        double maximumVerticalDistance,
        bool useDetachmentHeight)
    {
        var candidates = zones.ToArray();
        var direct = FindNearestVertically(
            candidates.Where(zone => targetX >= zone.Left && targetX < zone.Left + zone.Width),
            targetY,
            maximumVerticalDistance,
            useDetachmentHeight);
        if (direct is not null)
        {
            return direct;
        }

        var betweenGroups = new List<TileGroupDropZone>();
        foreach (var row in candidates.GroupBy(zone => Math.Round(zone.Top, 1)))
        {
            var ordered = row.OrderBy(zone => zone.Left).ToArray();
            for (var index = 0; index < ordered.Length - 1; index++)
            {
                var left = ordered[index];
                var right = ordered[index + 1];
                var leftEdge = left.Left + left.Width;
                if (targetX < leftEdge || targetX >= right.Left)
                {
                    continue;
                }

                betweenGroups.Add(targetX < (leftEdge + right.Left) / 2 ? left : right);
            }
        }

        return FindNearestVertically(betweenGroups, targetY, maximumVerticalDistance, useDetachmentHeight);
    }

    private static TileGroupDropZone? FindNearestVertically(
        IEnumerable<TileGroupDropZone> zones,
        double targetY,
        double maximumVerticalDistance,
        bool useDetachmentHeight)
    {
        var candidates = zones.ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var rows = candidates
            .GroupBy(zone => Math.Round(zone.Top, 1))
            .Select(row => new
            {
                Top = row.Min(zone => zone.Top),
                Bottom = row.Max(zone => Bottom(zone, useDetachmentHeight)),
            })
            .OrderBy(row => row.Top)
            .ToArray();
        for (var index = 0; index < rows.Length - 1; index++)
        {
            if (targetY > rows[index].Bottom + maximumVerticalDistance
                && targetY < rows[index + 1].Top - maximumVerticalDistance)
            {
                return null;
            }
        }

        var lastBottom = candidates.Max(zone => Bottom(zone, useDetachmentHeight));
        if (targetY > lastBottom + maximumVerticalDistance)
        {
            return null;
        }

        TileGroupDropZone? nearest = null;
        var nearestDistance = double.PositiveInfinity;
        foreach (var zone in candidates)
        {
            var bottom = Bottom(zone, useDetachmentHeight);
            var distance = targetY < zone.Top
                ? zone.Top - targetY
                : targetY > bottom
                    ? targetY - bottom
                    : 0;
            if (distance < nearestDistance)
            {
                nearest = zone;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static double Bottom(TileGroupDropZone zone, bool useDetachmentHeight)
    {
        var height = useDetachmentHeight && !double.IsNaN(zone.DetachmentHeight)
            ? zone.DetachmentHeight
            : zone.Height;
        return zone.Top + Math.Max(height, Win10TileMetrics.CellPitch);
    }

    private static int ClampColumn(double draggedLeft, double groupLeft, int columnSpan) =>
        Math.Clamp(
            (int)Math.Round((draggedLeft - groupLeft) / Win10TileMetrics.CellPitch),
            0,
            Win10TileMetrics.GroupColumns - columnSpan);

}