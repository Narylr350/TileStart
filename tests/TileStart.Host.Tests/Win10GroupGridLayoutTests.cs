using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10GroupGridLayoutTests
{
    [Fact]
    public void LegacyGroupsMigrateFromPackedOrderToColumnStacks()
    {
        var groups = Enumerable.Range(0, 5).Select(_ => new TileGroup()).ToArray();
        var layout = new TileLayout { Groups = [.. groups] };

        Assert.True(Win10GroupGridLayout.EnsureCoordinates(layout, 3));

        Assert.Equal(
            [
                new TileGroupCell(0, 0),
                new TileGroupCell(1, 0),
                new TileGroupCell(2, 0),
                new TileGroupCell(0, 1),
                new TileGroupCell(1, 1),
            ],
            groups.Select(Win10GroupGridLayout.GetCell));
    }

    [Fact]
    public void InsertingAGroupOnlyMovesLaterGroupsInTheSameColumn()
    {
        var upperLeft = Group(0, 0);
        var upperMiddle = Group(1, 0);
        var lowerLeft = Group(0, 1);
        var lowerMiddle = Group(1, 1);
        var inserted = new TileGroup();
        var layout = new TileLayout
        {
            Groups = [upperLeft, upperMiddle, lowerLeft, lowerMiddle, inserted],
        };

        Win10GroupGridLayout.Insert(layout, inserted, new TileGroupCell(0, 1), 3);

        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(upperLeft));
        Assert.Equal(new TileGroupCell(0, 1), Win10GroupGridLayout.GetCell(inserted));
        Assert.Equal(new TileGroupCell(0, 2), Win10GroupGridLayout.GetCell(lowerLeft));
        Assert.Equal(new TileGroupCell(1, 0), Win10GroupGridLayout.GetCell(upperMiddle));
        Assert.Equal(new TileGroupCell(1, 1), Win10GroupGridLayout.GetCell(lowerMiddle));
    }

    [Fact]
    public void MovingOntoAnOccupiedCellSwapsOnlyThoseGroups()
    {
        var first = Group(0, 0);
        var second = Group(1, 0);
        var belowFirst = Group(0, 1);
        var layout = new TileLayout { Groups = [first, second, belowFirst] };

        Assert.True(Win10GroupGridLayout.Move(layout, first, new TileGroupCell(1, 0), 3));

        Assert.Equal(new TileGroupCell(1, 0), Win10GroupGridLayout.GetCell(first));
        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(second));
        Assert.Equal(new TileGroupCell(0, 1), Win10GroupGridLayout.GetCell(belowFirst));
    }

    [Fact]
    public void RemovingAGroupOnlyCompactsItsOwnColumn()
    {
        var upperLeft = Group(0, 0);
        var upperMiddle = Group(1, 0);
        var lowerLeft = Group(0, 1);
        var lowerMiddle = Group(1, 1);
        var layout = new TileLayout { Groups = [upperLeft, upperMiddle, lowerLeft, lowerMiddle] };

        Assert.True(Win10GroupGridLayout.Remove(layout, upperLeft));

        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(lowerLeft));
        Assert.Equal(new TileGroupCell(1, 0), Win10GroupGridLayout.GetCell(upperMiddle));
        Assert.Equal(new TileGroupCell(1, 1), Win10GroupGridLayout.GetCell(lowerMiddle));
    }

    private static TileGroup Group(int column, int row) => new()
    {
        GroupColumn = column,
        GroupRow = row,
    };
}