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
    public const double ContextMenuFontSize = 12;
    public const double ContextMenuMinWidth = 256;
    public const double ContextMenuCheckPlaceholderWidth = 24;
    public const double ContextMenuIconPlaceholderWidth = 32;
    public const double TileGroupHeaderHeight = 32;
    public const double TileGroupHeaderFontSize = 14;
    public const double TileGroupGripperWidth = 48;
    public const double TileGroupGripperFontSize = 16;
    public const double TileGroupHeaderStrokeThickness = 2;
    public const double TileGroupPrimaryFocusThickness = 2;
    public const double TileGroupSecondaryFocusThickness = 1;
    public const double TileGroupHeaderToTilesSpacing = 3;
    public const double TileReservedBrandingSpace = 28;
    public const double TileLogoVerticalOffset = -2;
    public const double TileFolderHeaderHeight = 32;
    public const double TileFolderSeparatorHeight = 1;
    public const double TileFolderBottomMargin = 4;

    public static GridLength CollapsedNavigationGridLength { get; } = new(CollapsedNavigationWidth);

    public static GridLength NavigationItemGridLength { get; } = new(NavigationItemHeight);

    public static GridLength AllAppsGridLength { get; } = new(AllAppsWidth);

    public static GridLength ContextMenuCheckPlaceholderGridLength { get; } = new(ContextMenuCheckPlaceholderWidth);

    public static GridLength ContextMenuIconPlaceholderGridLength { get; } = new(ContextMenuIconPlaceholderWidth);

    public static GridLength TileGroupGripperGridLength { get; } = new(TileGroupGripperWidth);

    public static GridLength TileReservedBrandingGridLength { get; } = new(TileReservedBrandingSpace);

    public static Thickness AllAppsMargin { get; } = new(12, 0, 0, 0);

    public static Thickness AllAppsListPadding { get; } = new(0, 7, 0, 54);

    public static Thickness ContextMenuItemPadding { get; } = new(12, 7, 12, 7);

    public static Thickness ContextMenuPresenterPadding { get; } = new(0, 4, 0, 4);

    public static Thickness TileGroupHeaderMargin { get; } = new(4, 0, 0, 0);

    public static Thickness TileGroupHeaderBorderThickness { get; } = new(TileGroupHeaderStrokeThickness);

    public static Thickness TileGroupPrimaryFocusBorderThickness { get; } = new(TileGroupPrimaryFocusThickness);

    public static Thickness TileGroupSecondaryFocusBorderThickness { get; } = new(TileGroupSecondaryFocusThickness);

    public static Thickness TileGroupTitleRestMargin { get; } = new(0);

    public static Thickness TileGroupTitleInteractiveMargin { get; } = new(0);

    public static Thickness TileGroupGripperMargin { get; } = new(16, 6, 16, 6);

    public static Thickness TileNestedPanelMargin { get; } = new(4, 0, 4, 4);

    public static Thickness TileGroupTilesMargin { get; } = new(4, TileGroupHeaderToTilesSpacing, 4, 4);

    public static Thickness TileBrandingMargin { get; } = new(8, 0, 8, 5);
}