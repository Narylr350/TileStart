using System.Windows;

namespace TileStart.Host;

public static class Win10VisualMetrics
{
    public const double CollapsedNavigationWidth = 48;
    public const double ExpandedNavigationWidth = 256;
    public const double NavigationItemHeight = 48;
    public const double NavigationGlyphFontSize = 16;
    public const double NavigationTextFontSize = 15;
    public const double NavigationUserPictureSize = 20;
    public const double NavigationShadowBlurRadius = 10;
    public const double NavigationShadowDepth = 1.4142135623730951;
    public const double NavigationShadowDirection = 315;
    public const double NavigationShadowOpacity = 0.8;
    public const int NavigationBackdropOpenDurationMilliseconds = 350;
    public const int NavigationBackdropCloseDurationMilliseconds = 240;
    public const double Windows11TaskbarLeftInset = 8;
    public const double AllAppsWidth = 260;
    public const double AllAppsGridItemWidth = 244;
    public const double AllAppsHorizontalInset = (AllAppsWidth - AllAppsGridItemWidth) / 2;
    public const double AllAppsRowHeight = 36;
    public const double AllAppsGroupHeaderHeight = 36;
    public const double AllAppsGroupHeaderFontSize = 12;
    public const double AllAppsExpandCollapseCaretFontSize = 8;
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
    public const double TileNestedPanelHorizontalMargin = 4;

    public const double TileGroupVisualWidth =
        Win10TileMetrics.GroupWidth + TileNestedPanelHorizontalMargin * 2;

    public const double TileGroupVisualGap = Win10TileMetrics.GroupPitch - TileGroupVisualWidth;
    public const double TileReservedBrandingSpace = 28;
    public const double TileLogoVerticalOffset = -2;
    public const double TileFolderBottomMargin = 4;
    // StartUI TileStyles exposes a 14 DIP scrollbar slot and a 12 DIP mouse indicator.
    public const double TileScrollBarWidth = 14;
    public const double TileScrollBarThumbMouseWidth = 12;
    public const double TileScrollBarRightMargin = 0;
    public const double TileScrollBarLayoutWidth = TileScrollBarWidth + TileScrollBarRightMargin;
    public const double TileScrollViewerLeftMargin = 28;

    public static GridLength CollapsedNavigationGridLength { get; } = new(CollapsedNavigationWidth);

    // Windows 11 的左对齐任务栏在屏幕边缘保留 8 DIP。平移整个导航面板，
    // 让内容、按钮描边、Reveal 光效和点击区域保持同一坐标系；应用列表仍留在原位。
    public static double NavigationPaneHorizontalOffset { get; } =
        NavigationPaneOffsetForWindowsBuild(
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? 22000 : 0);

    public static Thickness NavigationPaneMargin { get; } =
        new(NavigationPaneHorizontalOffset, 0, 0, 0);

    public static double NavigationPaneOffsetForWindowsBuild(int build) =>
        build >= 22000 ? Windows11TaskbarLeftInset : 0;

    public static GridLength NavigationItemGridLength { get; } = new(NavigationItemHeight);

    public static GridLength AllAppsGridLength { get; } = new(AllAppsWidth);

    public static GridLength ContextMenuCheckPlaceholderGridLength { get; } = new(ContextMenuCheckPlaceholderWidth);

    public static GridLength ContextMenuIconPlaceholderGridLength { get; } = new(ContextMenuIconPlaceholderWidth);

    public static GridLength TileGroupGripperGridLength { get; } = new(TileGroupGripperWidth);

    public static GridLength TileReservedBrandingGridLength { get; } = new(TileReservedBrandingSpace);

    public static Thickness AllAppsMargin { get; } = new(12, 0, 0, 0);

    // Native StartUI applies this padding inside a deeper nested scrolling surface.
    // It is reference evidence, not a margin that can be copied onto TileStart's flat ListBox viewport.
    public static Thickness AllAppsListPadding { get; } = new(0, 7, 0, 54);

    // TileStart's current ListBox is the viewport itself, so only the verified top inset belongs here.
    public static Thickness AllAppsViewportMargin { get; } = new(0, 7, 0, 0);

    public static Thickness AllAppsItemMargin { get; } = new(AllAppsHorizontalInset, 0, AllAppsHorizontalInset, 0);

    public static Thickness ContextMenuItemPadding { get; } = new(12, 7, 12, 7);

    public static Thickness ContextMenuPresenterPadding { get; } = new(0, 4, 0, 4);

    public static Thickness TileGroupHeaderMargin { get; } =
        new(TileNestedPanelHorizontalMargin, 0, TileNestedPanelHorizontalMargin, 0);

    public static Thickness AllAppsGroupHeaderPadding { get; } = new(4, 0, 0, 10);

    public static Thickness TileGroupHeaderBorderThickness { get; } = new(TileGroupHeaderStrokeThickness);

    public static Thickness TileGroupPrimaryFocusBorderThickness { get; } = new(TileGroupPrimaryFocusThickness);

    public static Thickness TileGroupSecondaryFocusBorderThickness { get; } = new(TileGroupSecondaryFocusThickness);

    public static Thickness TileGroupTitleRestMargin { get; } = new(0);

    public static Thickness TileGroupTitleInteractiveMargin { get; } = new(0);

    public static Thickness TileGroupGripperMargin { get; } = new(16, 6, 16, 6);

    public static Thickness TileNestedPanelMargin { get; } =
        new(TileNestedPanelHorizontalMargin, 0, TileNestedPanelHorizontalMargin, 4);

    public static Thickness TileGroupTilesMargin { get; } =
        new(
            TileNestedPanelHorizontalMargin,
            TileGroupHeaderToTilesSpacing,
            TileNestedPanelHorizontalMargin,
            4);

    public static Thickness TileBrandingMargin { get; } = new(8, 0, 8, 5);

    public static Thickness TileTopBrandingMargin { get; } = new(8, 5, 8, 0);

    public static Thickness TileScrollBarMargin { get; } = new(0, 2, TileScrollBarRightMargin, 2);

    public static Thickness TileScrollViewerMargin { get; } = new(TileScrollViewerLeftMargin, 28, 0, 0);
}