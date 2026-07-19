using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileAreaDropResolverTests
{
    [Fact]
    public void NearbyBlankSpaceBelowAGroupStillTargetsThatGroupColumn()
    {
        var first = new TileGroupDropZone("first", 0, 0, 412, 100);
        var second = new TileGroupDropZone("second", 428, 0, 412, 300);

        var target = TileAreaDropResolver.FindTarget([first, second], 206, 140);

        Assert.Equal("first", target?.GroupId);
    }

    [Fact]
    public void FarBelowTheLastGroupCreatesANewGroupTarget()
    {
        var group = new TileGroupDropZone("first", 0, 0, 412, 100);

        Assert.Null(TileAreaDropResolver.FindTarget([group], 206, 160));
    }

    [Fact]
    public void DraggedTileCenterInsideTheBottomCreationBandStillTargetsTheGroup()
    {
        var group = new TileGroupDropZone("first", 0, 0, 412, 100);

        var target = TileAreaDropResolver.FindTargetForDraggedTile([group], 156, 55, 100, 100);

        Assert.Equal("first", target?.GroupId);
    }

    [Fact]
    public void DraggedTileCenterPastTheBottomCreationBandCreatesANewGroupTarget()
    {
        var group = new TileGroupDropZone("first", 0, 0, 412, 100);

        Assert.Null(TileAreaDropResolver.FindTargetForDraggedTile([group], 156, 80, 100, 100));
    }

    [Fact]
    public void DraggedTileUsesThePreDragHeightWhenItsPreviewExpandedTheLiveGroup()
    {
        var originalHeight = 204d;
        var expandedPreviewHeight = 308d;
        var group = new TileGroupDropZone(
            "first",
            0,
            0,
            412,
            expandedPreviewHeight,
            originalHeight);
        var draggedCenter = originalHeight + TileAreaDropResolver.NewGroupCreationBand + 1;

        Assert.Null(TileAreaDropResolver.FindTargetForDraggedTile(
            [group],
            156,
            draggedCenter - 50,
            100,
            100));
    }

    [Fact]
    public void DetachedGapBetweenVisualRowsCreatesANewGroupTarget()
    {
        var upper = new TileGroupDropZone("upper", 0, 0, 412, 204, 204, 0, 0);
        var lower = new TileGroupDropZone("lower", 0, 300, 412, 204, 204, 0, 1);

        Assert.Null(TileAreaDropResolver.FindTargetForDraggedTile([upper, lower], 156, 212, 100, 100));
    }

    [Fact]
    public void NewGroupTargetPreservesTheHorizontalCellAndInsertsBeforeTheFollowingRow()
    {
        var upper = new TileGroupDropZone("upper", 0, 0, 412, 204, 204, 0, 0);
        var lower = new TileGroupDropZone("lower", 0, 300, 412, 204, 204, 0, 1);

        var target = TileAreaDropResolver.FindNewGroupTargetForDraggedTile(
            [upper, lower],
            Win10TileMetrics.Left(6),
            212,
            100,
            columnSpan: 2,
            groupColumns: 3);

        Assert.Equal(new TileNewGroupDropTarget(0, 1, 6, 0), target);
    }

    [Fact]
    public void NewGroupTargetUsesTheMatchingOuterColumnWithoutTouchingItsNeighbors()
    {
        var upperLeft = new TileGroupDropZone("upper-left", 0, 0, 412, 204, 204, 0, 0);
        var lowerLeft = new TileGroupDropZone("lower-left", 0, 300, 412, 204, 204, 0, 1);
        var upperMiddle = new TileGroupDropZone("upper-middle", 428, 0, 412, 404, 404, 1, 0);

        var target = TileAreaDropResolver.FindNewGroupTargetForDraggedTile(
            [upperLeft, lowerLeft, upperMiddle],
            Win10TileMetrics.GroupPitch + Win10TileMetrics.Left(2),
            430,
            100,
            columnSpan: 2,
            groupColumns: 3);

        Assert.Equal(new TileNewGroupDropTarget(1, 1, 2, 0), target);
    }

    [Fact]
    public void SpaceOutsideExistingGroupColumnsCreatesNoGroupTarget()
    {
        var group = new TileGroupDropZone("first", 0, 0, 412, 100);

        Assert.Null(TileAreaDropResolver.FindTarget([group], 420, 260));
    }

    [Fact]
    public void WrappedRowsChooseTheNearestGroupAtThatHorizontalColumn()
    {
        var upper = new TileGroupDropZone("upper", 0, 0, 412, 100);
        var lower = new TileGroupDropZone("lower", 0, 400, 412, 100);

        Assert.Equal("lower", TileAreaDropResolver.FindTarget([upper, lower], 200, 430)?.GroupId);
    }

    [Fact]
    public void PointerInsideThirdExistingGroupTargetsThatGroup()
    {
        var first = new TileGroupDropZone("first", 0, 0, 412, 200);
        var second = new TileGroupDropZone("second", 428, 0, 412, 200);
        var third = new TileGroupDropZone("third", 856, 0, 412, 200);

        Assert.Equal("third", TileAreaDropResolver.FindTarget([first, second, third], 900, 100)?.GroupId);
    }

    [Theory]
    [InlineData(412, "first")]
    [InlineData(420, "second")]
    [InlineData(427.9, "second")]
    public void GapBetweenGroupsBelongsToAnAdjacentGroup(double pointerX, string expected)
    {
        var first = new TileGroupDropZone("first", 0, 0, 412, 200);
        var second = new TileGroupDropZone("second", 428, 0, 412, 200);

        Assert.Equal(expected, TileAreaDropResolver.FindTarget([first, second], pointerX, 100)?.GroupId);
    }
}