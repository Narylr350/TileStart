using System.Windows;

namespace TileStart.Host;

public static class Win10VisualMetrics
{
    public const double CollapsedNavigationWidth = 48;
    public const double NavigationItemHeight = 48;
    public const double AllAppsWidth = 260;
    public const double AllAppsRowHeight = 36;
    public const double AllAppsGroupHeaderHeight = 36;
    public const double AlphabetCellSize = 48;
    public const double AlphabetFontSize = 20;
    public const double TileGroupHeaderHeight = 32;

    public static GridLength CollapsedNavigationGridLength { get; } = new(CollapsedNavigationWidth);

    public static GridLength NavigationItemGridLength { get; } = new(NavigationItemHeight);

    public static GridLength AllAppsGridLength { get; } = new(AllAppsWidth);

    public static Thickness AllAppsMargin { get; } = new(12, 0, 0, 0);

    public static Thickness AllAppsListPadding { get; } = new(0, 7, 0, 54);

    public static Thickness TileNestedPanelMargin { get; } = new(4, 0, 4, 4);
}
