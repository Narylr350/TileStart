namespace TileStart.Host.Icons;

public static class Win10IconMetrics
{
    // The active Win10 installation visually matches the legacy 24-DIP branch.
    // Keep both symbol-derived branches below until the runtime feature flag is identified.
    public const double ClassicAppLogoImageSize = 24;
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
