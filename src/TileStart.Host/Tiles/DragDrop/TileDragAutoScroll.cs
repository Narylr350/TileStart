using TileStart.Host;

namespace TileStart.Host.Tiles.DragDrop;

public static class TileDragAutoScroll
{
    public const double EdgeZone = Win10TileMetrics.CellPitch;
    public const double MinimumSpeed = Win10TileMetrics.CellPitch * 2;
    public const double MaximumSpeed = Win10TileMetrics.CellPitch * 12;

    public static double GetVelocity(
        double pointerY,
        double viewportHeight,
        double verticalOffset,
        double scrollableHeight)
    {
        if (viewportHeight <= 0 || scrollableHeight <= 0)
        {
            return 0;
        }

        if (pointerY < EdgeZone && verticalOffset > 0)
        {
            return -Speed((EdgeZone - pointerY) / EdgeZone);
        }

        if (pointerY > viewportHeight - EdgeZone && verticalOffset < scrollableHeight)
        {
            return Speed((pointerY - (viewportHeight - EdgeZone)) / EdgeZone);
        }

        return 0;
    }

    public static double GetNextOffset(
        double currentOffset,
        double scrollableHeight,
        double velocity,
        double elapsedSeconds) =>
        Math.Clamp(currentOffset + velocity * Math.Max(0, elapsedSeconds), 0, Math.Max(0, scrollableHeight));

    private static double Speed(double intensity)
    {
        var amount = Math.Clamp(intensity, 0, 1);
        return MinimumSpeed + (MaximumSpeed - MinimumSpeed) * amount;
    }
}
