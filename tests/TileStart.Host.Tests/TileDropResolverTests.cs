using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileDropResolverTests
{
    [Fact]
    public void CellResolutionPreservesPointerAnchor()
    {
        var tile = new TileItem { Size = TileSize.Medium };

        var cell = TileDropResolver.GetCell(
            new System.Windows.Point(166, 62),
            new System.Windows.Point(62, 10),
            tile);

        Assert.Equal((2, 1), cell);
    }

    [Fact]
    public void FolderTargetUsesDraggedTileCenterInsteadOfRawPointer()
    {
        var target = new TileItem { Name = "target", Size = TileSize.Medium, Column = 2, Row = 0 };
        var moving = new TileItem { Name = "moving", Size = TileSize.Medium, Column = 0, Row = 0 };
        var group = new TileGroup { Tiles = [target, moving] };

        var resolved = TileDropResolver.FindFolderTarget(
            group,
            moving,
            new System.Windows.Point(110, 10),
            new System.Windows.Point(10, 10));

        Assert.Same(target, resolved);
    }

    [Fact]
    public void FolderTilesCannotBeNested()
    {
        var target = new TileItem { Name = "target", Size = TileSize.Medium, Column = 2, Row = 0 };
        var moving = new TileItem { Name = "folder", IsTileFolder = true, Size = TileSize.Medium };
        var group = new TileGroup { Tiles = [target, moving] };

        Assert.Null(TileDropResolver.FindFolderTarget(
            group,
            moving,
            new System.Windows.Point(110, 10),
            new System.Windows.Point(10, 10)));
    }

    [Fact]
    public void ReflowDelayMatchesStartUiBinary()
    {
        Assert.Equal(120, TileDropResolver.ReflowDelayMilliseconds);
    }
}
