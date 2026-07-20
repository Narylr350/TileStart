using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class StartWindowSizingTests
{
    [Fact]
    public void WidthTargetsExposeExactlyOneTwoOrThreeGroupColumns()
    {
        for (var columns = 1; columns <= 3; columns++)
        {
            var viewportWidth = StartWindowSizing.WidthForColumns(columns)
                                - Win10VisualMetrics.CollapsedNavigationWidth
                                - Win10VisualMetrics.AllAppsWidth
                                - Win10VisualMetrics.TileScrollViewerLeftMargin;

            Assert.Equal(columns, Win10GroupWrapPanel.ColumnsForWidth(viewportWidth));
            Assert.True(viewportWidth >= Win10GroupWrapPanel.RequiredWidth(columns));
        }
    }

    [Theory]
    [InlineData(800, 1)]
    [InlineData(1100, 2)]
    [InlineData(1500, 3)]
    public void WidthSnapsToNearestAvailableColumnTarget(double requestedWidth, int expectedColumns)
    {
        Assert.Equal(
            StartWindowSizing.WidthForColumns(expectedColumns),
            StartWindowSizing.SnapWidth(requestedWidth, double.PositiveInfinity));
    }

    [Fact]
    public void WorkAreaExcludesTargetsThatCannotFit()
    {
        var twoColumns = StartWindowSizing.WidthForColumns(2);
        var availableWidth = twoColumns + 20;

        Assert.Equal(twoColumns, StartWindowSizing.SnapWidth(10_000, availableWidth));
        Assert.Equal(twoColumns, StartWindowSizing.MaximumWidth(availableWidth));
    }

    [Theory]
    [InlineData(300, 480)]
    [InlineData(700, 700)]
    [InlineData(1200, 900)]
    public void HeightRemainsContinuousWithinTheWorkArea(double requestedHeight, double expectedHeight)
    {
        Assert.Equal(expectedHeight, StartWindowSizing.ClampHeight(requestedHeight, 480, 900));
    }
}
