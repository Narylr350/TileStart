namespace TileStart.Host;

public readonly record struct TileGroupDropZone(
    string GroupId,
    double Left,
    double Top,
    double Width,
    double Height);

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
        return FindTarget(zones, pointerX, pointerY, NewGroupDetachmentDistance);
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
            NewGroupCreationBand);
    }

    private static TileGroupDropZone? FindTarget(
        IEnumerable<TileGroupDropZone> zones,
        double targetX,
        double targetY,
        double maximumVerticalDistance)
    {
        var candidates = zones.ToArray();
        var direct = FindNearestVertically(
            candidates.Where(zone => targetX >= zone.Left && targetX < zone.Left + zone.Width),
            targetY,
            maximumVerticalDistance);
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

        return FindNearestVertically(betweenGroups, targetY, maximumVerticalDistance);
    }

    private static TileGroupDropZone? FindNearestVertically(
        IEnumerable<TileGroupDropZone> zones,
        double targetY,
        double maximumVerticalDistance)
    {
        var candidates = zones.ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var lastBottom = candidates.Max(Bottom);
        if (targetY > lastBottom + maximumVerticalDistance)
        {
            return null;
        }

        TileGroupDropZone? nearest = null;
        var nearestDistance = double.PositiveInfinity;
        foreach (var zone in candidates)
        {
            var bottom = Bottom(zone);
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

    private static double Bottom(TileGroupDropZone zone) =>
        zone.Top + Math.Max(zone.Height, Win10TileMetrics.CellPitch);
}