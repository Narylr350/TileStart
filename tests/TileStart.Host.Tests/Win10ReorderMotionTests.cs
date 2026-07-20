using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10ReorderMotionTests
{
    [Fact]
    public void GridAffectedMotionUsesRecoveredThemeParameters()
    {
        Assert.Equal(400, Win10ReorderMotion.DurationMilliseconds);
        Assert.Equal(0.1, Win10ReorderMotion.Spline.ControlPoint1.X, 3);
        Assert.Equal(0.9, Win10ReorderMotion.Spline.ControlPoint1.Y, 3);
        Assert.Equal(0.2, Win10ReorderMotion.Spline.ControlPoint2.X, 3);
        Assert.Equal(1, Win10ReorderMotion.Spline.ControlPoint2.Y, 3);
    }

    [Fact]
    public void RetargetDeltaPreservesTheCurrentVisiblePosition()
    {
        var delta = Win10ReorderMotion.ResolveRetargetDelta(
            new System.Windows.Point(300, 200),
            new System.Windows.Point(260, 220),
            new System.Windows.Vector(20, -10));

        Assert.Equal(new System.Windows.Vector(60, -30), delta);
    }

    [Fact]
    public void DragCompletionParametersSeparateMeasuredHandoffFromCancelApproximation()
    {
        Assert.Equal(50, Win10ReorderMotion.DropHandoffDurationMilliseconds);
        Assert.Equal(200, Win10ReorderMotion.CancelReturnDurationMilliseconds);
    }
}
