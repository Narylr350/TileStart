using System.Windows.Media.Animation;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10InteractionMotionTests
{
    [Fact]
    public void PressTransitionMatchesRecoveredUxThemeParameters()
    {
        Assert.Equal(0.975, Win10InteractionMotion.PressedScale);
        Assert.Equal(167, Win10InteractionMotion.PressTransitionDurationMilliseconds);
        Assert.Equal(0.1, Win10InteractionMotion.PressSplineControlPoint1.X);
        Assert.Equal(0.9, Win10InteractionMotion.PressSplineControlPoint1.Y);
        Assert.Equal(0.2, Win10InteractionMotion.PressSplineControlPoint2.X);
        Assert.Equal(1, Win10InteractionMotion.PressSplineControlPoint2.Y);
    }

    [Fact]
    public void ScaleAnimationStartsAtCurrentValueAndUsesRecoveredSpline()
    {
        var animation = Win10InteractionMotion.CreateScaleAnimation(0.99, Win10InteractionMotion.PressedScale);

        Assert.Equal(TimeSpan.FromMilliseconds(167), animation.Duration.TimeSpan);
        Assert.Equal(FillBehavior.Stop, animation.FillBehavior);
        var start = Assert.IsType<DiscreteDoubleKeyFrame>(animation.KeyFrames[0]);
        var end = Assert.IsType<SplineDoubleKeyFrame>(animation.KeyFrames[1]);
        Assert.Equal(0.99, start.Value);
        Assert.Equal(TimeSpan.Zero, start.KeyTime.TimeSpan);
        Assert.Equal(Win10InteractionMotion.PressedScale, end.Value);
        Assert.Equal(TimeSpan.FromMilliseconds(167), end.KeyTime.TimeSpan);
        Assert.Equal(Win10InteractionMotion.PressSplineControlPoint1.X, end.KeySpline.ControlPoint1.X);
        Assert.Equal(Win10InteractionMotion.PressSplineControlPoint1.Y, end.KeySpline.ControlPoint1.Y);
        Assert.Equal(Win10InteractionMotion.PressSplineControlPoint2.X, end.KeySpline.ControlPoint2.X);
        Assert.Equal(Win10InteractionMotion.PressSplineControlPoint2.Y, end.KeySpline.ControlPoint2.Y);
    }

    [Theory]
    [InlineData(50, 50, true)]
    [InlineData(-40, 50, true)]
    [InlineData(140, 50, true)]
    [InlineData(-51, 50, false)]
    [InlineData(151, 50, false)]
    public void SharedPointerLightCanIlluminateAContainerFromOutside(double x, double y, bool expected)
    {
        var actual = Win10InteractionMotion.IsPointerWithinRevealRadius(
            new System.Windows.Point(x, y),
            new System.Windows.Size(100, 100),
            50);

        Assert.Equal(expected, actual);
    }
}