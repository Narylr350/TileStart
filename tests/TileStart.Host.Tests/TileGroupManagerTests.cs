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
    public void MoveChangesGroupOrderAndRejectsEdges()
    {
        var first = new TileGroup { Name = "一" };
        var second = new TileGroup { Name = "二" };
        var third = new TileGroup { Name = "三" };
        var layout = new TileLayout { Groups = [first, second, third] };

        Assert.True(TileGroupManager.Move(layout, third, -1));
        Assert.Equal([first, third, second], layout.Groups);
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
                new TileGroup { Name = "开发" },
                new TileGroup { Name = "游戏" },
            ],
        };

        var restored = TileLayoutStore.Deserialize(TileLayoutStore.Serialize(layout));

        Assert.Equal(["开发", "游戏"], restored!.Groups.Select(group => group.Name));
    }
}
