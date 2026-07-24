namespace TileStart.Host.Tests;

public sealed class SmoothScrollTests
{
    [Fact]
    public void WheelDistanceUsesWindowsLineCount()
    {
        Assert.Equal(108, SmoothScroll.CalculateWheelDistance(3, 600));
    }

    [Fact]
    public void PageScrollingUsesMostOfTheViewport()
    {
        Assert.Equal(510, SmoothScroll.CalculateWheelDistance(-1, 600));
    }

    [Fact]
    public void InterpolationApproachesTargetWithoutOvershooting()
    {
        var first = SmoothScroll.InterpolateOffset(0, 100, 8);
        var second = SmoothScroll.InterpolateOffset(first, 100, 8);

        Assert.InRange(first, 0.01, 99.99);
        Assert.InRange(second, first, 99.99);
    }

    [Theory]
    [InlineData(5, 149, false)]
    [InlineData(5, 150, true)]
    [InlineData(0.8, 20, true)]
    public void SnapStopsTheTailWithoutInterruptingRecentInput(
        double remainingDistance,
        double millisecondsSinceLastInput,
        bool expected)
    {
        Assert.Equal(expected, SmoothScroll.ShouldSnapToTarget(remainingDistance, millisecondsSinceLastInput));
    }
}
