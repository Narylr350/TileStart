namespace TileStart.Host;

public readonly record struct TileGroupDropZone(
    string GroupId,
    double Left,
    double Top,
    double Width,
    double Height);

public static class TileAreaDropResolver
{
    public static TileGroupDropZone? FindTarget(
        IEnumerable<TileGroupDropZone> zones,
        double pointerX,
        double pointerY)
    {
        var candidates = zones.ToArray();
        var direct = FindNearestVertically(
            candidates.Where(zone => pointerX >= zone.Left && pointerX < zone.Left + zone.Width),
            pointerY);
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
                if (pointerX < leftEdge || pointerX >= right.Left)
                {
                    continue;
                }

                betweenGroups.Add(pointerX < (leftEdge + right.Left) / 2 ? left : right);
            }
        }

        return FindNearestVertically(betweenGroups, pointerY);
    }

    private static TileGroupDropZone? FindNearestVertically(
        IEnumerable<TileGroupDropZone> zones,
        double pointerY)
    {
        TileGroupDropZone? nearest = null;
        var nearestDistance = double.PositiveInfinity;
        foreach (var zone in zones)
        {
            var bottom = zone.Top + Math.Max(zone.Height, Win10TileMetrics.CellPitch);
            var distance = pointerY < zone.Top
                ? zone.Top - pointerY
                : pointerY > bottom
                    ? pointerY - bottom
                    : 0;
            if (distance < nearestDistance)
            {
                nearest = zone;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}
