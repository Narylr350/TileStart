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
    public void TileFolderTimingsMatchDenseRuntimeFrameEvidence()
    {
        Assert.Equal(180, Win10FolderMotion.TilePreviewExitDurationMilliseconds);
        Assert.Equal(240, Win10FolderMotion.TilePreviewEnterDurationMilliseconds);
        Assert.Equal(300, Win10FolderMotion.TileChildDurationMilliseconds);
        Assert.Equal(30, Win10FolderMotion.TileChildWaveDelayMilliseconds);
        Assert.Equal(280, Win10FolderMotion.TileDecorationDelayMilliseconds);
        Assert.True(Win10FolderMotion.StandardSpline.IsFrozen);
        Assert.True(Win10FolderMotion.TileExpandShiftSpline.IsFrozen);
        Assert.Equal(0.1, Win10FolderMotion.StandardSpline.ControlPoint1.X, 3);
        Assert.Equal(0.9, Win10FolderMotion.StandardSpline.ControlPoint1.Y, 3);
        Assert.Equal(0.9, Win10FolderMotion.TileExpandShiftSpline.ControlPoint1.X, 3);
        Assert.Equal(0.1, Win10FolderMotion.TileExpandShiftSpline.ControlPoint1.Y, 3);
    }

    [Fact]
    public void TileFolderChildrenEnterInBottomRightToTopLeftDiagonalWaves()
    {
        Assert.Equal(0, Win10FolderMotion.TileChildWaveDelay(2, 2, 3, 3));
        Assert.Equal(30, Win10FolderMotion.TileChildWaveDelay(1, 2, 3, 3));
        Assert.Equal(30, Win10FolderMotion.TileChildWaveDelay(2, 1, 3, 3));
        Assert.Equal(60, Win10FolderMotion.TileChildWaveDelay(0, 2, 3, 3));
        Assert.Equal(60, Win10FolderMotion.TileChildWaveDelay(1, 1, 3, 3));
        Assert.Equal(120, Win10FolderMotion.TileChildWaveDelay(0, 0, 3, 3));
        Assert.Equal(420, Win10FolderMotion.TileChildrenDuration(3, 3));
        Assert.Equal(0, Win10FolderMotion.TileChildrenDuration(0, 0));
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