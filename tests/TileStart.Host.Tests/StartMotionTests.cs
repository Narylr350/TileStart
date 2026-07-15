namespace TileStart.Host.Tests;

public sealed class StartMotionTests
{
    [Theory]
    [InlineData(0, 60)]
    [InlineData(0.5, 115)]
    [InlineData(1, 170)]
    public void CalculateEntrance_UsesRecoveredBottomTaskbarFallback(double position, double fromY)
    {
        var result = StartMotion.CalculateEntrance(position, true);

        Assert.Equal(fromY, result.FromY);
        Assert.Equal(17, result.DelayMilliseconds);
        Assert.Equal(500, result.DurationMilliseconds);
    }

    [Fact]
    public void CalculateEntrance_DoesNotInventVerticalMotionForOtherTaskbarEdges()
    {
        var result = StartMotion.CalculateEntrance(0.5, false);

        Assert.Equal(0, result.FromY);
        Assert.Equal(0, result.DurationMilliseconds);
    }
}
