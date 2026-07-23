using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10GroupWrapPanelTests
{
    [Fact]
    public void TwelveWorkspaceUnitsFitAtCurrentSavedTilePaneWidth()
    {
        Assert.Equal(12, Win10GroupWrapPanel.ColumnsForWidth(1275.33));
        Assert.Equal(1273.33, Win10GroupWrapPanel.RequiredWidth(12), precision: 2);
    }

    [Fact]
    public void WrapCalculationDoesNotChargeTrailingGroupGap()
    {
        Assert.Equal(8, Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(8)));
        Assert.Equal(12, Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(12)));
    }

    [Fact]
    public void OverlayClearanceUsesTheMeasuredRuntimeWidths()
    {
        Assert.Equal(
            6,
            Win10GroupWrapPanel.OverlayClearanceDeficit(
                viewportWidth: 1275.33,
                columns: 12,
                scrollBarFootprint: 8),
            precision: 2);
        Assert.Equal(
            0,
            Win10GroupWrapPanel.OverlayClearanceDeficit(
                viewportWidth: 1284,
                columns: 12,
                scrollBarFootprint: 8));
    }

    [Fact]
    public void TrulyNarrowerViewportWrapsInsteadOfClippingTheThirdGroup()
    {
        Assert.Equal(
            11,
            Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(12) - 8));
    }

    [Fact]
    public void ShelfRowsUseTheTallestGroupInThePreviousRow()
    {
        var slots = Win10GroupWrapPanel.CalculateSlots(
            [
                new Win10GroupPanelItem(0, 0, 0, 1, 100, 232),
                new Win10GroupPanelItem(1, 1, 0, 1, 100, 432),
                new Win10GroupPanelItem(2, 0, 1, 1, 100, 232),
                new Win10GroupPanelItem(3, 1, 1, 1, 100, 232),
            ],
            columns: 2);

        Assert.Equal(432, slots[2].Top);
        Assert.Equal(432, slots[3].Top);
    }

    [Fact]
    public void VariableWidthGroupsShareAWorkspaceRowWithoutOverlap()
    {
        var slots = Win10GroupWrapPanel.CalculateSlots(
            [
                new Win10GroupPanelItem(0, 0, 0, 3, TileWorkspaceMetrics.GroupVisualWidth(3), 240),
                new Win10GroupPanelItem(1, 3, 0, 1, TileWorkspaceMetrics.GroupVisualWidth(1), 140),
            ],
            columns: 4);

        Assert.Equal(0, slots[0].Left);
        Assert.Equal(TileWorkspaceMetrics.Left(3), slots[1].Left);
        Assert.True(slots[0].Left + slots[0].Width <= slots[1].Left);
    }
}
