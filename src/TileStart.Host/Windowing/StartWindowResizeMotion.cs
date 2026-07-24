namespace TileStart.Host.Windowing;

public static class StartWindowResizeMotion
{
    public const int DurationMilliseconds = 180;

    public static double Interpolate(double from, double to, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        return from + (to - from) * eased;
    }
}
