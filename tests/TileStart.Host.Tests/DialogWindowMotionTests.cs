using System.Windows.Media.Animation;
using TileStart.Host.Windowing;

namespace TileStart.Host.Tests;

public sealed class DialogWindowMotionTests
{
    [Fact]
    public void OpenTransitionUsesTheFluentDecelerateSpline()
    {
        var animation = DialogWindowMotion.CreateAnimation(
            0,
            1,
            DialogWindowMotion.OpenDurationMilliseconds,
            DialogWindowMotion.OpenSpline);

        Assert.Equal(TimeSpan.FromMilliseconds(320), animation.Duration.TimeSpan);
        Assert.Equal(0, Assert.IsType<DiscreteDoubleKeyFrame>(animation.KeyFrames[0]).Value);
        var destination = Assert.IsType<SplineDoubleKeyFrame>(animation.KeyFrames[1]);
        Assert.Equal(1, destination.Value);
        Assert.Equal(new System.Windows.Point(0.16, 0.8), destination.KeySpline.ControlPoint1);
        Assert.Equal(new System.Windows.Point(0.25, 1), destination.KeySpline.ControlPoint2);
    }

    [Fact]
    public void CloseTransitionIsShorterThanTheOpenTransition()
    {
        var animation = DialogWindowMotion.CreateAnimation(
            1,
            0,
            DialogWindowMotion.CloseDurationMilliseconds,
            DialogWindowMotion.CloseSpline);

        Assert.Equal(TimeSpan.FromMilliseconds(180), animation.Duration.TimeSpan);
        Assert.True(DialogWindowMotion.CloseDurationMilliseconds < DialogWindowMotion.OpenDurationMilliseconds);
        var destination = Assert.IsType<SplineDoubleKeyFrame>(animation.KeyFrames[1]);
        Assert.Equal(new System.Windows.Point(0.7, 0), destination.KeySpline.ControlPoint1);
        Assert.Equal(new System.Windows.Point(1, 0.5), destination.KeySpline.ControlPoint2);
    }
}
