using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class StartWindowResizeMotionTests
{
    [Fact]
    public void InterpolationStartsAndEndsAtExactWidths()
    {
        Assert.Equal(100, StartWindowResizeMotion.Interpolate(100, 500, 0));
        Assert.Equal(500, StartWindowResizeMotion.Interpolate(100, 500, 1));
    }

    [Fact]
    public void EaseOutAdvancesPastLinearHalfwayPoint()
    {
        Assert.True(StartWindowResizeMotion.Interpolate(0, 100, 0.5) > 50);
    }

    [Fact]
    public void ProgressIsClampedOutsideTheAnimationRange()
    {
        Assert.Equal(100, StartWindowResizeMotion.Interpolate(100, 500, -1));
        Assert.Equal(500, StartWindowResizeMotion.Interpolate(100, 500, 2));
    }
}
