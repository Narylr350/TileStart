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

    [Theory]
    [InlineData(48, 48, 0.8958, 0.8958)]
    [InlineData(100, 100, 0.95, 0.95)]
    [InlineData(204, 100, 0.9755, 0.95)]
    [InlineData(204, 204, 0.9755, 0.9755)]
    public void TilePressedScaleMatchesStartUiForEveryTileSize(
        double width,
        double height,
        double expectedX,
        double expectedY)
    {
        var scale = Win10InteractionMotion.TilePressedScale(new System.Windows.Size(width, height));

        Assert.Equal(expectedX, scale.Width);
        Assert.Equal(expectedY, scale.Height);
    }

    [Theory]
    [InlineData(48, 48, 1.08)]
    [InlineData(100, 100, 1.04)]
    [InlineData(204, 100, 1.04)]
    [InlineData(204, 204, 1.0)]
    public void TileDragScaleMatchesStartUiForEveryTileSize(double width, double height, double expected)
    {
        Assert.Equal(expected, Win10InteractionMotion.TileDragScale(new System.Windows.Size(width, height)));
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

    [Theory]
    [InlineData(8, 72, 48, 8)]
    [InlineData(80, 40, 20, 10)]
    [InlineData(0, 40, 40, 0)]
    public void RevealCornerRadiusFollowsThemeWithoutExceedingHalfTheControl(
        double requested,
        double width,
        double height,
        double expected)
    {
        var actual = Win10InteractionMotion.ConstrainCornerRadius(
            new System.Windows.CornerRadius(requested),
            new System.Windows.Size(width, height));

        Assert.Equal(expected, actual);
    }
}