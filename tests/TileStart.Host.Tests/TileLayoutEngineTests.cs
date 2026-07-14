using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileLayoutEngineTests
{
    [Theory]
    [InlineData(TileSize.Small, 1, 1, 48, 48)]
    [InlineData(TileSize.Medium, 2, 2, 100, 100)]
    [InlineData(TileSize.Wide, 4, 2, 204, 100)]
    [InlineData(TileSize.Large, 4, 4, 204, 204)]
    public void TileSizesMatchWin10Grid(TileSize size, int columns, int rows, double width, double height)
    {
        var tile = new TileItem { Size = size };

        Assert.Equal(columns, size.ColumnSpan());
        Assert.Equal(rows, size.RowSpan());
        Assert.Equal(width, tile.PixelWidth);
        Assert.Equal(height, tile.PixelHeight);
    }

    [Fact]
    public void NormalizeMovesCollidingAndOverflowingTilesToFirstAvailableCells()
    {
        var first = Tile(TileSize.Medium, 0, 0);
        var colliding = Tile(TileSize.Wide, 0, 0);
        var overflowing = Tile(TileSize.Large, 7, 0);
        var group = new TileGroup { Tiles = [first, colliding, overflowing] };

        TileLayoutEngine.Normalize(group);

        Assert.Equal((0, 0), (first.Column, first.Row));
        Assert.Equal((2, 0), (colliding.Column, colliding.Row));
        Assert.Equal((0, 2), (overflowing.Column, overflowing.Row));
        AssertNoOverlap(group);
    }

    [Fact]
    public void TryMoveRejectsOverlapAndGroupOverflow()
    {
        var stationary = Tile(TileSize.Medium, 0, 0);
        var moving = Tile(TileSize.Medium, 2, 0);
        var group = new TileGroup { Tiles = [stationary, moving] };

        Assert.False(TileLayoutEngine.TryMove(group, moving, 1, 0));
        Assert.False(TileLayoutEngine.TryMove(group, moving, 7, 0));
        Assert.True(TileLayoutEngine.TryMove(group, moving, 4, 1));
        Assert.Equal((4, 1), (moving.Column, moving.Row));
    }

    [Fact]
    public void MoveWithinGroupKeepsDropPositionAndAvoidsCollisions()
    {
        var moving = Tile(TileSize.Medium, 4, 0);
        var stationary = Tile(TileSize.Medium, 0, 0);
        var group = new TileGroup { Tiles = [stationary, moving] };

        Assert.True(TileLayoutEngine.Move(group, group, moving, 0, 0));

        Assert.Equal((0, 0), (moving.Column, moving.Row));
        Assert.Equal((2, 0), (stationary.Column, stationary.Row));
        AssertNoOverlap(group);
    }

    [Fact]
    public void MoveAcrossGroupsRemovesFromSourceAndAvoidsTargetCollisions()
    {
        var moving = Tile(TileSize.Wide, 0, 0);
        var source = new TileGroup { Tiles = [moving] };
        var stationary = Tile(TileSize.Medium, 0, 0);
        var target = new TileGroup { Tiles = [stationary] };

        Assert.True(TileLayoutEngine.Move(source, target, moving, 0, 0));

        Assert.Empty(source.Tiles);
        Assert.Equal((0, 0), (moving.Column, moving.Row));
        Assert.Equal((4, 0), (stationary.Column, stationary.Row));
        AssertNoOverlap(target);
    }

    [Fact]
    public void InvalidCrossGroupMoveLeavesSourceUnchanged()
    {
        var moving = Tile(TileSize.Large, 0, 0);
        var source = new TileGroup { Tiles = [moving] };
        var target = new TileGroup();

        Assert.False(TileLayoutEngine.Move(source, target, moving, 7, 0));

        Assert.Same(moving, Assert.Single(source.Tiles));
        Assert.Empty(target.Tiles);
    }

    [Fact]
    public void LayoutJsonRoundTripPreservesGroupsTilesSizesAndPositions()
    {
        var layout = new TileLayout
        {
            Groups =
            [
                new TileGroup
                {
                    Name = "工具",
                    Tiles =
                    [
                        new TileItem { Name = "终端", LaunchTarget = "wt.exe", Size = TileSize.Wide, Column = 2, Row = 3 },
                    ],
                },
            ],
        };

        var restored = TileLayoutStore.Deserialize(TileLayoutStore.Serialize(layout));

        var group = Assert.Single(restored!.Groups);
        var tile = Assert.Single(group.Tiles);
        Assert.Equal("工具", group.Name);
        Assert.Equal("终端", tile.Name);
        Assert.Equal("wt.exe", tile.LaunchTarget);
        Assert.Equal(TileSize.Wide, tile.Size);
        Assert.Equal((2, 3), (tile.Column, tile.Row));
    }

    private static TileItem Tile(TileSize size, int column, int row)
    {
        return new TileItem { Name = Guid.NewGuid().ToString(), Size = size, Column = column, Row = row };
    }

    private static void AssertNoOverlap(TileGroup group)
    {
        for (var firstIndex = 0; firstIndex < group.Tiles.Count; firstIndex++)
        {
            var first = group.Tiles[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < group.Tiles.Count; secondIndex++)
            {
                var second = group.Tiles[secondIndex];
                var overlaps = first.Column < second.Column + second.Size.ColumnSpan()
                    && first.Column + first.Size.ColumnSpan() > second.Column
                    && first.Row < second.Row + second.Size.RowSpan()
                    && first.Row + first.Size.RowSpan() > second.Row;
                Assert.False(overlaps, $"{first.Name} overlaps {second.Name}");
            }
        }
    }
}
