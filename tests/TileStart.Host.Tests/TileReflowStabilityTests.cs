using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileReflowStabilityTests
{
    [Fact]
    public void FirstCandidateStartsTheTimer()
    {
        var stability = new TileReflowStability();

        Assert.True(stability.Observe("group:0:0", new System.Windows.Point(100, 100)));
    }

    [Fact]
    public void MovementWithinThreeDipsKeepsTheCurrentTimer()
    {
        var stability = new TileReflowStability();
        stability.Observe("group:0:0", new System.Windows.Point(100, 100));

        Assert.False(stability.Observe("group:0:0", new System.Windows.Point(103, 100)));
    }

    [Fact]
    public void MovementBeyondThreeDipsRestartsTheTimerEvenInsideTheSameCell()
    {
        var stability = new TileReflowStability();
        stability.Observe("group:0:0", new System.Windows.Point(100, 100));

        Assert.True(stability.Observe("group:0:0", new System.Windows.Point(103.1, 100)));
    }

    [Fact]
    public void CustomDriftAllowsFolderCandidateMicroMovement()
    {
        var stability = new TileReflowStability(12);
        stability.Observe("folder:target", new System.Windows.Point(100, 100));

        Assert.False(stability.Observe("folder:target", new System.Windows.Point(111.9, 100)));
        Assert.True(stability.Observe("folder:target", new System.Windows.Point(112.1, 100)));
    }

    [Fact]
    public void ChangingTheCandidateRestartsTheTimerWithoutPointerMovement()
    {
        var stability = new TileReflowStability();
        stability.Observe("group:0:0", new System.Windows.Point(100, 100));

        Assert.True(stability.Observe("group:2:0", new System.Windows.Point(100, 100)));
    }

    [Fact]
    public void ResetMakesTheSameCandidateStartANewTimer()
    {
        var stability = new TileReflowStability();
        stability.Observe("group:0:0", new System.Windows.Point(100, 100));
        stability.Reset();

        Assert.True(stability.Observe("group:0:0", new System.Windows.Point(100, 100)));
    }
}
