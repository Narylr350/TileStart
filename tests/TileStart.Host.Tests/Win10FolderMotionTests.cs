using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10FolderMotionTests
{
    [Fact]
    public void TileExpansionUsesRecoveredReverseRowAndColumnTiming()
    {
        Assert.Equal(111, Win10FolderMotion.TileShiftDuration(true, 0, 0, 5, 4));
        Assert.Equal(100, Win10FolderMotion.TileShiftDuration(true, 4, 3, 5, 4));
        Assert.Equal(115, Win10FolderMotion.TileShiftDuration(true, 0, 0, 1, 1));
    }

    [Fact]
    public void TileCollapseUsesRecoveredForwardRowAndColumnTiming()
    {
        Assert.Equal(300, Win10FolderMotion.TileShiftDuration(false, 0, 0, 5, 4));
        Assert.Equal(598, Win10FolderMotion.TileShiftDuration(false, 4, 3, 5, 4));
        Assert.Equal(300, Win10FolderMotion.TileShiftDuration(false, 0, 0, 1, 1));
    }

    [Fact]
    public void TileRegionTimingsMatchRecoveredStoryboards()
    {
        Assert.Equal(0, Win10FolderMotion.TileRegionExpandDelayMilliseconds);
        Assert.Equal(250, Win10FolderMotion.TileRegionExpandDurationMilliseconds);
        Assert.Equal(200, Win10FolderMotion.TileRegionCollapseDurationMilliseconds);
        Assert.Equal(0.1, Win10FolderMotion.StandardSpline.ControlPoint1.X, 3);
        Assert.Equal(0.9, Win10FolderMotion.StandardSpline.ControlPoint1.Y, 3);
        Assert.Equal(0.9, Win10FolderMotion.TileExpandShiftSpline.ControlPoint1.X, 3);
        Assert.Equal(0.1, Win10FolderMotion.TileExpandShiftSpline.ControlPoint1.Y, 3);
    }

    [Fact]
    public void AppFolderChildrenUseShortStaggerWhileReflowOwnsTheVisibleDuration()
    {
        Assert.Equal(0, Win10FolderMotion.AppChildDelay(0));
        Assert.Equal(34, Win10FolderMotion.AppChildDelay(2));
        Assert.Equal(201, Win10FolderMotion.AppChildrenDuration(3));
        Assert.Equal(400, Win10FolderMotion.AppOpenDuration(3));
        Assert.Equal(490, Win10FolderMotion.AppOpenDuration(20));
    }
}