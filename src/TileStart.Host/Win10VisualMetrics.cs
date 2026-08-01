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
    public const double ContextMenuSeparatorHeight = 1;
    // SystemControlDisabledBaseLowBrush resolves to #33 alpha in Win10 light and dark themes.
    public const double ContextMenuDisabledOpacity = 0.2;
    public const double ContextMenuSubmenuArrowFontSize = 12;
    public const string ContextMenuSubmenuArrowGlyph = "\uE0E3";
    public const double ContextMenuCheckGlyphFontSize = 16;
    public const string ContextMenuCheckGlyph = "\uE001";
    public const double TileGroupHeaderHeight = 32;
    public const double TileGroupHeaderFontSize = 14;
    public const double TileGroupGripperWidth = 48;
    public const double TileGroupGripperFontSize = 16;
    public const double TileGroupHeaderStrokeThickness = 2;
    public const double TileGroupPrimaryFocusThickness = 2;
    public const double TileGroupSecondaryFocusThickness = 1;
    public const double TilePrimaryFocusThickness = 2;
    public const double TileSecondaryFocusThickness = 1;
    public const double TileFocusVisualOffset = -2;
    public const double TileRevealBorderThickness = 2;
    // StartUI::TileViewControl::UpdateTileOpacity 将当前可重排或正在拖动的磁贴设为 0.8；
    // 0.5 属于仅受全局重排影响的其他磁贴，不能用于 TileStart 的拖动源。
    public const double TileDraggingOpacity = 0.8;
    public const double TileGroupHeaderToTilesSpacing = 3;
    public const double TileNestedPanelHorizontalMargin = 4;

    public const double TileGroupVisualWidth =
        Win10TileMetrics.GroupWidth + TileNestedPanelHorizontalMargin * 2;

    public const double TileGroupVisualGap = Win10TileMetrics.GroupPitch - TileGroupVisualWidth;
    public const double TileReservedBrandingSpace = 28;
    public const double TileLogoVerticalOffset = -2;
    public const double TileFolderBottomMargin = 4;
    // StartUI uses a 14 DIP slot. The system template renders a 16 DIP minimum thumb at
    // 1/8 scale and shifts it inward by 2 DIP, then removes the offset for MouseIndicator.
    public const double TileScrollBarWidth = 14;
    public const double TileScrollBarThumbMouseWidth = 12;
    public const double TileScrollBarThumbMinHeight = 16;
    public const double TileScrollBarThumbRestInset = 2;
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

    // Win11 只需要把导航内容对齐到左对齐任务栏的 8 DIP 内缩；展开背景仍必须贴住
    // 屏幕左边缘，否则会在 Acrylic 面板左侧露出一条与偏移等宽的空隙。
    public static Thickness NavigationBackdropMargin { get; } =
        NavigationBackdropMarginForWindowsBuild(
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? 22000 : 0);

    public static double NavigationPaneOffsetForWindowsBuild(int build) =>
        build >= 22000 ? Windows11TaskbarLeftInset : 0;

    public static Thickness NavigationBackdropMarginForWindowsBuild(int build) =>
        new(-NavigationPaneOffsetForWindowsBuild(build), 0, 0, 0);

    public static GridLength NavigationItemGridLength { get; } = new(NavigationItemHeight);

    public static GridLength AllAppsGridLength { get; } = new(AllAppsWidth);

    public static GridLength ContextMenuCheckPlaceholderGridLength { get; } = new(ContextMenuCheckPlaceholderWidth);

    public static GridLength ContextMenuIconPlaceholderGridLength { get; } = new(ContextMenuIconPlaceholderWidth);

    public static GridLength TileGroupGripperGridLength { get; } = new(TileGroupGripperWidth);

    public static GridLength TileReservedBrandingGridLength { get; } = new(TileReservedBrandingSpace);

    public static Thickness AllAppsMargin { get; } = new(12, 0, 0, 0);

    // StartUI 将这组留白应用到滚动内容本身；TileStart 也必须挂在 ItemsPanel 上，
    // 让底部 54 DIP 随内容滚动，而不是缩短 ListBox 的可视区域。
    public static Thickness AllAppsListPadding { get; } = new(0, 7, 0, 54);

    public static Thickness AllAppsItemMargin { get; } = new(AllAppsHorizontalInset, 0, AllAppsHorizontalInset, 0);

    public static Thickness ContextMenuItemPadding { get; } = new(12, 7, 12, 7);

    public static Thickness ContextMenuPresenterPadding { get; } = new(0, 4, 0, 4);

    public static Thickness ContextMenuSeparatorMargin { get; } = new(12, 4, 12, 4);

    public static Thickness TileGroupHeaderMargin { get; } =
        new(TileNestedPanelHorizontalMargin, 0, TileNestedPanelHorizontalMargin, 0);

    public static Thickness AllAppsGroupHeaderPadding { get; } = new(4, 0, 0, 10);

    public static Thickness TileGroupHeaderBorderThickness { get; } = new(TileGroupHeaderStrokeThickness);

    public static Thickness TileGroupPrimaryFocusBorderThickness { get; } = new(TileGroupPrimaryFocusThickness);

    public static Thickness TileGroupSecondaryFocusBorderThickness { get; } = new(TileGroupSecondaryFocusThickness);

    public static Thickness TilePrimaryFocusBorderThickness { get; } = new(TilePrimaryFocusThickness);

    public static Thickness TileSecondaryFocusBorderThickness { get; } = new(TileSecondaryFocusThickness);

    public static Thickness TileFocusVisualMargin { get; } = new(TileFocusVisualOffset);

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

    public static Thickness TileScrollBarThumbRestMargin { get; } = new(0, 0, TileScrollBarThumbRestInset, 0);

    public static Thickness TileScrollViewerMargin { get; } = new(TileScrollViewerLeftMargin, 28, 0, 0);
}