using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class WindowSizeStoreTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LegacyWidthAddsOneScrollBarSafetyGapWithoutChangingTheRequestedColumnCount(int columns)
    {
        var fixedWindowWidth = Win10VisualMetrics.CollapsedNavigationWidth
                               + Win10VisualMetrics.AllAppsWidth
                               + Win10VisualMetrics.TileScrollViewerLeftMargin;
        var savedWindowWidth = fixedWindowWidth + Win10GroupWrapPanel.RequiredWidth(columns);

        var migratedWidth = WindowSizeStore.MigrateWidth(savedWindowWidth, version: 0);
        var tileViewportWidth = migratedWidth - fixedWindowWidth;
        var rightSafetyGap = tileViewportWidth - Win10GroupWrapPanel.RequiredWidth(columns);

        Assert.Equal(
            savedWindowWidth + Win10VisualMetrics.TileScrollBarLayoutWidth,
            migratedWidth,
            precision: 8);
        Assert.Equal(columns, Win10GroupWrapPanel.ColumnsForWidth(tileViewportWidth));
        Assert.Equal(Win10VisualMetrics.TileScrollBarLayoutWidth, rightSafetyGap, precision: 8);
        Assert.Equal(
            migratedWidth,
            WindowSizeStore.MigrateWidth(migratedWidth, WindowSizeStore.CurrentFormatVersion));
    }
}
