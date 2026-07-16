namespace TileStart.Host;

public static class Win10IconMetrics
{
    public const double ClassicAppLogoImageSize = 16;
    public const double ClassicAppLogoLayoutSize = 24;

    public static double GetAppListImageSize(int appItemLogoType, bool themeAware) =>
        (appItemLogoType, themeAware) switch
        {
            (1, true) => 16,
            (1, false) => 24,
            (2 or 3, true) => 24,
            (2 or 3, false) => 32,
            _ => throw new ArgumentOutOfRangeException(nameof(appItemLogoType)),
        };

    public static double GetAppListLayoutSize(int appItemLogoType) =>
        appItemLogoType switch
        {
            1 => 24,
            2 or 3 => 32,
            _ => throw new ArgumentOutOfRangeException(nameof(appItemLogoType)),
        };
}
