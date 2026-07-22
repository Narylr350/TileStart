using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10MenuPopupMotionTests
{
    [Fact]
    public void TimingsAndCurveMatchMenuPopupThemeTransition()
    {
        Assert.Equal(0.5, Win10MenuPopupMotion.TopLevelClosedRatio, 2);
        Assert.Equal(0.67, Win10MenuPopupMotion.SubmenuClosedRatio, 2);
        Assert.Equal(250, Win10MenuPopupMotion.OpenDurationMilliseconds);
        Assert.Equal(83, Win10MenuPopupMotion.CloseOpacityDurationMilliseconds);
        Assert.Equal(0, Win10MenuPopupMotion.OpenSpline.ControlPoint1.X, 3);
        Assert.Equal(0, Win10MenuPopupMotion.OpenSpline.ControlPoint1.Y, 3);
        Assert.Equal(0, Win10MenuPopupMotion.OpenSpline.ControlPoint2.X, 3);
        Assert.Equal(1, Win10MenuPopupMotion.OpenSpline.ControlPoint2.Y, 3);
    }

    [Fact]
    public void DownwardSubmenuStartsWithBottomThirdVisible()
    {
        var clip = Win10MenuPopupMotion.InitialClip(
            200,
            100,
            Win10MenuPopupMotion.SubmenuClosedRatio,
            popupOpensUpward: false);

        Assert.Equal(0, clip.X);
        Assert.Equal(67, clip.Y, 2);
        Assert.Equal(200, clip.Width);
        Assert.Equal(33, clip.Height, 2);
    }

    [Fact]
    public void UpwardSubmenuReversesTheRevealDirection()
    {
        var clip = Win10MenuPopupMotion.InitialClip(
            200,
            100,
            Win10MenuPopupMotion.SubmenuClosedRatio,
            popupOpensUpward: true);

        Assert.Equal(0, clip.Y);
        Assert.Equal(33, clip.Height, 2);
    }

    [Fact]
    public void TopLevelMenuStartsWithHalfItsHeightVisible()
    {
        var clip = Win10MenuPopupMotion.InitialClip(
            200,
            100,
            Win10MenuPopupMotion.TopLevelClosedRatio,
            popupOpensUpward: false);

        Assert.Equal(50, clip.Y, 2);
        Assert.Equal(50, clip.Height, 2);
    }
}
