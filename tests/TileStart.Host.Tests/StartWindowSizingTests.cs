using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class StartWindowSizingTests
{
    [Fact]
    public void CleanInstallStartsWithTheTileWorkspaceCollapsed()
    {
        Assert.Equal(0, StartWindowSizing.DefaultWorkspaceColumns);
        Assert.Equal(StartWindowSizing.MinimumGroupColumns, StartWindowSizing.DefaultWorkspaceColumns);
    }

    [Fact]
    public void EmptyLayoutIgnoresAPreviouslySavedExpandedWorkspace()
    {
        Assert.Equal(0, StartWindowSizing.InitialWorkspaceColumns(3, 0, 0));
    }

    [Fact]
    public void ExistingTilesRestoreTheSavedWorkspaceWithoutViolatingTheirMinimumWidth()
    {
        Assert.Equal(3, StartWindowSizing.InitialWorkspaceColumns(3, 1, 4));
        Assert.Equal(2, StartWindowSizing.InitialWorkspaceColumns(1, 1, 5));
    }

    [Fact]
    public void AllAppsOnlyWidthDoesNotReserveTheTilePaneInset()
    {
        Assert.Equal(
            Win10VisualMetrics.CollapsedNavigationWidth + Win10VisualMetrics.AllAppsWidth,
            StartWindowSizing.WidthForColumns(0));
    }

    [Fact]
    public void WidthTargetsExposeTheRequestedWorkspaceWidth()
    {
        for (var columns = 0; columns <= 3; columns++)
        {
            var viewportWidth = StartWindowSizing.WidthForColumns(columns)
                                - Win10VisualMetrics.CollapsedNavigationWidth
                                - Win10VisualMetrics.AllAppsWidth;

            var workspaceColumns = columns * TileWorkspaceMetrics.LegacyGroupWidthUnits;
            if (workspaceColumns == 0)
            {
                Assert.Equal(0, viewportWidth);
                continue;
            }

            viewportWidth -= Win10VisualMetrics.TileScrollViewerLeftMargin;

            Assert.Equal(workspaceColumns, Win10GroupWrapPanel.ColumnsForWidth(viewportWidth));
            Assert.True(viewportWidth >= Win10GroupWrapPanel.RequiredWidth(workspaceColumns));
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
    public void WidthCanSnapToTheAllAppsOnlyLayout()
    {
        Assert.Equal(
            StartWindowSizing.WidthForColumns(0),
            StartWindowSizing.SnapWidth(400, double.PositiveInfinity));
    }

    [Fact]
    public void WorkAreaExcludesTargetsThatCannotFit()
    {
        var twoColumns = StartWindowSizing.WidthForColumns(2);
        var availableWidth = twoColumns + 20;

        Assert.Equal(twoColumns, StartWindowSizing.SnapWidth(10_000, availableWidth));
        Assert.Equal(twoColumns, StartWindowSizing.MaximumWidth(availableWidth));
    }

    [Fact]
    public void WorkAreaNarrowerThanTheFixedPaneUsesTheActualAvailableWidth()
    {
        var availableWidth = StartWindowSizing.WidthForColumns(0) - 40;

        Assert.Equal(availableWidth, StartWindowSizing.SnapWidth(10_000, availableWidth));
        Assert.Equal(availableWidth, StartWindowSizing.MaximumWidth(availableWidth));
    }

    [Fact]
    public void FullHdAt150PercentCanExposeTwoWorkspaceColumns()
    {
        var logicalWorkWidth = 1920 / 1.5;
        var maximumWidth = StartWindowSizing.MaximumWidth(logicalWorkWidth);

        Assert.Equal(2, StartWindowSizing.ColumnsForWidth(maximumWidth, logicalWorkWidth));
    }

    [Fact]
    public void FullHdAt175PercentCanOnlyExposeOneWorkspaceColumn()
    {
        var logicalWorkWidth = 1920 / 1.75;
        var maximumWidth = StartWindowSizing.MaximumWidth(logicalWorkWidth);

        Assert.Equal(1, StartWindowSizing.ColumnsForWidth(maximumWidth, logicalWorkWidth));
    }

    [Theory]
    [InlineData(300, 480, 900)]
    [InlineData(700, 700, 900)]
    [InlineData(1200, 900, 900)]
    [InlineData(700, 400, 400)]
    public void HeightRemainsContinuousWithinTheWorkArea(
        double requestedHeight,
        double expectedHeight,
        double availableHeight)
    {
        Assert.Equal(expectedHeight, StartWindowSizing.ClampHeight(requestedHeight, 480, availableHeight));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(12, 1)]
    public void MinimumWidthDependsOnWhetherTilesExist(int tileCount, int expectedColumns)
    {
        Assert.Equal(expectedColumns, StartWindowSizing.MinimumColumnsForTileCount(tileCount));
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 4, 1)]
    [InlineData(1, 5, 2)]
    [InlineData(8, 8, 2)]
    public void MinimumWidthIncludesTheWidestVisibleGroup(
        int tileCount,
        int widestGroupWidthUnits,
        int expectedWorkspaceColumns)
    {
        Assert.Equal(
            expectedWorkspaceColumns,
            StartWindowSizing.MinimumColumnsForTileLayout(tileCount, widestGroupWidthUnits));
    }

    [Fact]
    public void InvalidWidthFallsBackToTheMinimumWorkspace()
    {
        Assert.Equal(StartWindowSizing.MinimumGroupColumns, StartWindowSizing.ColumnsForWidth(double.NaN));
        Assert.Equal(
            StartWindowSizing.MinimumGroupColumns,
            StartWindowSizing.ColumnsForWidth(double.PositiveInfinity));
    }
}