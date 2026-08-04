namespace TileStart.Host.Tests;

public sealed class RenderFrameProbeTests
{
    [Theory]
    [InlineData(649, 650, true)]
    [InlineData(650, 650, true)]
    [InlineData(651, 650, false)]
    public void FramesAreRecordedOnlyInsideTheRequestedDuration(
        int elapsedMilliseconds,
        int durationMilliseconds,
        bool expected)
    {
        Assert.Equal(
            expected,
            RenderFrameProbe.IsWithinDuration(
                TimeSpan.FromMilliseconds(elapsedMilliseconds),
                TimeSpan.FromMilliseconds(durationMilliseconds)));
    }
}
