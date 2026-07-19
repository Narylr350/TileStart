using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileGroupManagerTests
{
    [Fact]
    public void AddAppendsNamedGroup()
    {
        var layout = new TileLayout();

        var group = TileGroupManager.Add(layout, "工具");

        Assert.Same(group, Assert.Single(layout.Groups));
        Assert.Equal("工具", group.Name);
    }

    [Fact]
    public void MoveSwapsAdjacentColumnsAndRejectsEdges()
    {
        var first = new TileGroup { Name = "一", GroupColumn = 0, GroupRow = 0 };
        var second = new TileGroup { Name = "二", GroupColumn = 1, GroupRow = 0 };
        var third = new TileGroup { Name = "三", GroupColumn = 2, GroupRow = 0 };
        var layout = new TileLayout { Groups = [first, second, third] };

        Assert.True(TileGroupManager.Move(layout, third, -1));
        Assert.Equal(new TileGroupCell(1, 0), Win10GroupGridLayout.GetCell(third));
        Assert.Equal(new TileGroupCell(2, 0), Win10GroupGridLayout.GetCell(second));
        Assert.False(TileGroupManager.Move(layout, first, -1));
        Assert.False(TileGroupManager.Move(layout, second, 1));
    }

    [Fact]
    public void RemoveDeletesSelectedGroup()
    {
        var first = new TileGroup();
        var second = new TileGroup();
        var layout = new TileLayout { Groups = [first, second] };

        Assert.True(TileGroupManager.Remove(layout, first));

        Assert.Same(second, Assert.Single(layout.Groups));
    }

    [Fact]
    public void JsonRoundTripPreservesGroupOrderAndNames()
    {
        var layout = new TileLayout
        {
            Groups =
            [
                new TileGroup { Name = "开发", GroupColumn = 0, GroupRow = 0 },
                new TileGroup { Name = "游戏", GroupColumn = 1, GroupRow = 0 },
            ],
        };

        var restored = TileLayoutStore.Deserialize(TileLayoutStore.Serialize(layout));

        Assert.Equal(["开发", "游戏"], restored!.Groups.Select(group => group.Name));
        Assert.Equal(
            [new TileGroupCell(0, 0), new TileGroupCell(1, 0)],
            restored.Groups.Select(Win10GroupGridLayout.GetCell));
    }

    [Fact]
    public void LegacyJsonWithoutOuterCellsMigratesFromItsSavedOrder()
    {
        const string json = """
                            {
                              "Groups": [
                                { "Name": "一", "Tiles": [] },
                                { "Name": "二", "Tiles": [] },
                                { "Name": "三", "Tiles": [] },
                                { "Name": "四", "Tiles": [] }
                              ]
                            }
                            """;
        var layout = TileLayoutStore.Deserialize(json)!;

        Assert.All(layout.Groups, group => Assert.Equal(new TileGroupCell(-1, -1), Win10GroupGridLayout.GetCell(group)));
        Assert.True(Win10GroupGridLayout.EnsureCoordinates(layout, columns: 3));
        Assert.Equal(
            [
                new TileGroupCell(0, 0),
                new TileGroupCell(1, 0),
                new TileGroupCell(2, 0),
                new TileGroupCell(0, 1),
            ],
            layout.Groups.Select(Win10GroupGridLayout.GetCell));
    }
}
