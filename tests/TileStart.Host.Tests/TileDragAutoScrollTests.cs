using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileDragAutoScrollTests
{
    [Fact]
    public void PointerOutsideTheEdgeZonesDoesNotScroll()
    {
        Assert.Equal(0, TileDragAutoScroll.GetVelocity(300, 600, 100, 1000));
    }

    [Fact]
    public void TopAndBottomEdgeZonesScrollInOppositeDirections()
    {
        Assert.True(TileDragAutoScroll.GetVelocity(10, 600, 100, 1000) < 0);
        Assert.True(TileDragAutoScroll.GetVelocity(590, 600, 100, 1000) > 0);
    }

    [Fact]
    public void ScrollBoundariesStopFurtherMovement()
    {
        Assert.Equal(0, TileDragAutoScroll.GetVelocity(10, 600, 0, 1000));
        Assert.Equal(0, TileDragAutoScroll.GetVelocity(590, 600, 1000, 1000));
    }

    [Fact]
    public void OffsetUsesElapsedTimeAndClampsToTheScrollableRange()
    {
        Assert.Equal(126, TileDragAutoScroll.GetNextOffset(100, 1000, 520, 0.05));
        Assert.Equal(0, TileDragAutoScroll.GetNextOffset(10, 1000, -520, 1));
        Assert.Equal(1000, TileDragAutoScroll.GetNextOffset(990, 1000, 520, 1));
    }
}
