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
        TileGroupDropZone? nearest = null;
        var nearestDistance = double.PositiveInfinity;
        foreach (var zone in zones)
        {
            if (pointerX < zone.Left || pointerX >= zone.Left + zone.Width)
            {
                continue;
            }

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
