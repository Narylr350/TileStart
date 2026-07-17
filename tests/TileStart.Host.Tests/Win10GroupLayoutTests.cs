using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10GroupLayoutTests
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

    [Theory]
    [InlineData(300, 1)]
    [InlineData(427, 1)]
    [InlineData(428, 1)]
    [InlineData(855, 1)]
    [InlineData(856, 2)]
    [InlineData(1284, 3)]
    public void GroupsWrapAtWholeGroupPitch(double availableWidth, int expectedGroupsPerRow)
    {
        Assert.Equal(16, Win10TileMetrics.GroupGap);
        Assert.Equal(428, Win10TileMetrics.GroupPitch);
        Assert.Equal(expectedGroupsPerRow, Win10TileMetrics.GroupsPerRow(availableWidth));
    }

    [Fact]
    public void NativeWin10LayoutUsesEightColumnMetricsWithoutOverlap()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", "native-layout.xml");
        var document = System.Xml.Linq.XDocument.Load(path);
        var layoutOptions = document.Descendants().Single(element => element.Name.LocalName == "LayoutOptions");
        Assert.Equal(Win10TileMetrics.GroupColumns, (int)layoutOptions.Attribute("StartTileGroupCellWidth")!);
        Assert.Equal(412, Win10TileMetrics.GroupWidth);

        foreach (var groupElement in document.Descendants().Where(element => element.Name.LocalName == "Group"))
        {
            var tiles = groupElement.Elements()
                .Select(element => new
                {
                    Size = ParseNativeSize((string)element.Attribute("Size")!),
                    Column = (int)element.Attribute("Column")!,
                    Row = (int)element.Attribute("Row")!,
                })
                .ToArray();

            for (var index = 0; index < tiles.Length; index++)
            {
                var tile = tiles[index];
                Assert.InRange(tile.Column, 0, Win10TileMetrics.GroupColumns - tile.Size.ColumnSpan());
                Assert.True(tile.Row >= 0);

                var bounds = Win10TileMetrics.Bounds(tile.Size, tile.Column, tile.Row);
                Assert.Equal(tile.Column * Win10TileMetrics.CellPitch, bounds.Left);
                Assert.Equal(tile.Row * Win10TileMetrics.CellPitch, bounds.Top);
                Assert.True(bounds.Left + bounds.Width <= Win10TileMetrics.GroupWidth);

                for (var otherIndex = index + 1; otherIndex < tiles.Length; otherIndex++)
                {
                    Assert.False(Overlaps(tile.Size, tile.Column, tile.Row,
                                          tiles[otherIndex].Size, tiles[otherIndex].Column, tiles[otherIndex].Row));
                }
            }
        }
    }

    [Fact]
    public void NormalizeMovesCollidingAndOverflowingTilesToFirstAvailableCells()
    {
        var first = Tile(TileSize.Medium, 0, 0);
        var colliding = Tile(TileSize.Wide, 0, 0);
        var overflowing = Tile(TileSize.Large, 7, 0);
        var group = new TileGroup { Tiles = [first, colliding, overflowing] };

        Win10GroupLayout.Normalize(group);

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

        Assert.False(Win10GroupLayout.TryMove(group, moving, 1, 0));
        Assert.False(Win10GroupLayout.TryMove(group, moving, 7, 0));
        Assert.True(Win10GroupLayout.TryMove(group, moving, 4, 1));
        Assert.Equal((4, 1), (moving.Column, moving.Row));
    }

    [Fact]
    public void AddKeepsDropPositionAndMovesCollidingTiles()
    {
        var stationary = Tile(TileSize.Medium, 0, 0);
        var added = Tile(TileSize.Wide, 0, 0);
        var group = new TileGroup { Tiles = [stationary] };

        Assert.True(Win10GroupLayout.Add(group, added, 0, 0));

        Assert.Equal((0, 0), (added.Column, added.Row));
        Assert.Equal((4, 0), (stationary.Column, stationary.Row));
        AssertNoOverlap(group);
    }

    [Fact]
    public void MoveWithinGroupKeepsDropPositionAndAvoidsCollisions()
    {
        var moving = Tile(TileSize.Medium, 4, 0);
        var stationary = Tile(TileSize.Medium, 0, 0);
        var group = new TileGroup { Tiles = [stationary, moving] };

        Assert.True(Win10GroupLayout.Move(group, group, moving, 0, 0));

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

        Assert.True(Win10GroupLayout.Move(source, target, moving, 0, 0));

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

        Assert.False(Win10GroupLayout.Move(source, target, moving, 7, 0));

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
                        new TileItem
                        {
                            Name = "终端",
                            LaunchTarget = "wt.exe",
                            TargetType = TileTargetType.Application,
                            Arguments = "-w 0",
                            WorkingDirectory = @"C:\Work",
                            IconPath = @"C:\Icons\terminal.ico",
                            RunAsAdministrator = true,
                            Size = TileSize.Wide,
                            Column = 2,
                            Row = 3,
                        },
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
        Assert.Equal(TileTargetType.Application, tile.TargetType);
        Assert.Equal("-w 0", tile.Arguments);
        Assert.Equal(@"C:\Work", tile.WorkingDirectory);
        Assert.Equal(@"C:\Icons\terminal.ico", tile.IconPath);
        Assert.True(tile.RunAsAdministrator);
        Assert.Equal(TileSize.Wide, tile.Size);
        Assert.Equal((2, 3), (tile.Column, tile.Row));
    }

    [Fact]
    public void LayoutJsonRoundTripPreservesTileFolders()
    {
        var child = new TileItem { Name = "child", LaunchTarget = "child.exe", Size = TileSize.Small };
        var folder = new TileItem
        {
            Name = "文件夹",
            IsTileFolder = true,
            Size = TileSize.Medium,
            FolderTiles = [child],
        };
        var layout = new TileLayout { Groups = [new TileGroup { Tiles = [folder] }] };

        var restored = TileLayoutStore.Deserialize(TileLayoutStore.Serialize(layout));

        var restoredFolder = Assert.Single(Assert.Single(restored!.Groups).Tiles);
        Assert.True(restoredFolder.IsTileFolder);
        Assert.Equal("child.exe", Assert.Single(restoredFolder.FolderTiles).LaunchTarget);
    }

    private static TileSize ParseNativeSize(string size) => size switch
    {
        "1x1" => TileSize.Small,
        "2x2" => TileSize.Medium,
        "4x2" => TileSize.Wide,
        "4x4" => TileSize.Large,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null),
    };

    private static bool Overlaps(
        TileSize firstSize,
        int firstColumn,
        int firstRow,
        TileSize secondSize,
        int secondColumn,
        int secondRow) =>
        firstColumn < secondColumn + secondSize.ColumnSpan()
        && firstColumn + firstSize.ColumnSpan() > secondColumn
        && firstRow < secondRow + secondSize.RowSpan()
        && firstRow + firstSize.RowSpan() > secondRow;

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
