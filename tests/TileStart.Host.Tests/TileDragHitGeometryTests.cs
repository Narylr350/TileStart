using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileDragHitGeometryTests
{
    [Fact]
    public void SnapshotKeepsInsertionAtTheOriginalFollowingRow()
    {
        var zones = new[]
        {
            new TileGroupDropZone("first", 0, 0, 412, 204, GroupColumn: 0, GroupRow: 0),
            new TileGroupDropZone("second", 0, 300, 412, 204, GroupColumn: 0, GroupRow: 1),
            new TileGroupDropZone("third", 0, 600, 412, 204, GroupColumn: 0, GroupRow: 2),
        };
        var geometry = new TileDragHitGeometry(zones);

        zones[2] = zones[2] with { Top = 900, GroupRow = 3 };

        var target = geometry.FindNewGroupTarget(0, 500, 100, columnSpan: 2, groupColumns: 1);
        var unstableLiveTarget = TileAreaDropResolver.FindNewGroupTargetForDraggedTile(
            zones,
            0,
            500,
            100,
            columnSpan: 2,
            groupColumns: 1);

        Assert.Equal(2, target.GroupRow);
        Assert.Equal(3, unstableLiveTarget.GroupRow);
    }

    [Fact]
    public void SnapshotKeepsTheOriginalDetachmentHeight()
    {
        var zones = new[]
        {
            new TileGroupDropZone("first", 0, 0, 412, 204, GroupColumn: 0, GroupRow: 0),
        };
        var geometry = new TileDragHitGeometry(zones);

        zones[0] = zones[0] with { Height = 412 };
        var draggedCenter = 204 + TileAreaDropResolver.NewGroupCreationBand + 1;

        Assert.Null(geometry.FindTarget(156, draggedCenter - 50, 100, 100));
        Assert.NotNull(TileAreaDropResolver.FindTargetForDraggedTile(
            zones,
            156,
            draggedCenter - 50,
            100,
            100));
    }

    [Fact]
    public void UpdatedPositionsRecognizeTheProvisionalGroupInsteadOfTheFormerLowerGroup()
    {
        var geometry = new TileDragHitGeometry(
        [
            new TileGroupDropZone("upper", 0, 0, 412, 204, GroupColumn: 0, GroupRow: 0),
            new TileGroupDropZone("lower", 0, 204, 412, 204, GroupColumn: 0, GroupRow: 1),
        ]);

        geometry.Update(
        [
            new TileGroupDropZone("upper", 0, 0, 412, 204, GroupColumn: 0, GroupRow: 0),
            new TileGroupDropZone("provisional", 0, 204, 412, 100, GroupColumn: 0, GroupRow: 1),
            new TileGroupDropZone("lower", 0, 408, 412, 204, GroupColumn: 0, GroupRow: 2),
        ]);

        var target = geometry.FindTarget(0, 204, 100, 100);

        Assert.Equal("provisional", target?.GroupId);
    }

    [Fact]
    public void UpdatingPositionsStillKeepsTheInitialDetachmentHeight()
    {
        var geometry = new TileDragHitGeometry(
        [
            new TileGroupDropZone("first", 0, 0, 412, 204, GroupColumn: 0, GroupRow: 0),
        ]);
        geometry.Update(
        [
            new TileGroupDropZone("first", 0, 0, 412, 412, GroupColumn: 0, GroupRow: 0),
        ]);
        var draggedCenter = 204 + TileAreaDropResolver.NewGroupCreationBand + 1;

        Assert.Null(geometry.FindTarget(156, draggedCenter - 50, 100, 100));
    }

    [Fact]
    public void ShortGroupAcceptsDropsInsideTheVisualHeightOfItsOuterRow()
    {
        var geometry = new TileDragHitGeometry(
        [
            new TileGroupDropZone("short", 0, 0, 412, 100, GroupColumn: 0, GroupRow: 0),
            new TileGroupDropZone("tall", 428, 0, 412, 204, GroupColumn: 4, GroupRow: 0),
        ]);

        var target = geometry.FindTarget(156, 104, 100, 100);

        Assert.Equal("short", target?.GroupId);
    }
}