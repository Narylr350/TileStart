using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10GroupWrapPanelTests
{
    [Fact]
    public void ThreeGroupsFitAtCurrentSavedTilePaneWidth()
    {
        Assert.Equal(3, Win10GroupWrapPanel.ColumnsForWidth(1275.33));
        Assert.Equal(1276, Win10GroupWrapPanel.RequiredWidth(3));
    }

    [Fact]
    public void WrapCalculationDoesNotChargeTrailingGroupGap()
    {
        Assert.Equal(2, Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(2)));
        Assert.Equal(3, Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(3)));
    }

    [Fact]
    public void OverlayClearanceUsesTheMeasuredRuntimeWidths()
    {
        Assert.Equal(
            8.67,
            Win10GroupWrapPanel.OverlayClearanceDeficit(
                viewportWidth: 1275.33,
                columns: 3,
                scrollBarFootprint: 8),
            precision: 2);
        Assert.Equal(
            0,
            Win10GroupWrapPanel.OverlayClearanceDeficit(
                viewportWidth: 1284,
                columns: 3,
                scrollBarFootprint: 8));
    }

    [Fact]
    public void TrulyNarrowerViewportWrapsInsteadOfClippingTheThirdGroup()
    {
        Assert.Equal(
            2,
            Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(3) - 8));
    }

    [Fact]
    public void EachColumnAccumulatesItsOwnGroupHeights()
    {
        var slots = Win10GroupWrapPanel.CalculateSlots(
            [
                new Win10GroupPanelItem(0, 0, 0, 232),
                new Win10GroupPanelItem(1, 1, 0, 432),
                new Win10GroupPanelItem(2, 0, 1, 232),
                new Win10GroupPanelItem(3, 1, 1, 232),
            ],
            columns: 2);

        Assert.Equal(232, slots[2].Top);
        Assert.Equal(432, slots[3].Top);
    }
}
