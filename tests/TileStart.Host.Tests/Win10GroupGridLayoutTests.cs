using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10GroupGridLayoutTests
{
    [Fact]
    public void LegacyGroupsMigrateFromPackedOrderToWorkspaceRows()
    {
        var groups = Enumerable.Range(0, 5).Select(_ => new TileGroup()).ToArray();
        var layout = new TileLayout { Groups = [.. groups] };

        Assert.True(Win10GroupGridLayout.EnsureCoordinates(layout, 12));

        Assert.Equal(
            [
                new TileGroupCell(0, 0),
                new TileGroupCell(4, 0),
                new TileGroupCell(8, 0),
                new TileGroupCell(0, 1),
                new TileGroupCell(4, 1),
            ],
            groups.Select(Win10GroupGridLayout.GetCell));
    }

    [Fact]
    public void InsertingIntoAnOccupiedRegionUsesTheNearestFreeWorkspaceCell()
    {
        var upperLeft = Group(0, 0);
        var upperMiddle = Group(4, 0);
        var lowerLeft = Group(0, 1);
        var lowerMiddle = Group(4, 1);
        var inserted = new TileGroup();
        var layout = new TileLayout
        {
            Groups = [upperLeft, upperMiddle, lowerLeft, lowerMiddle, inserted],
        };

        Win10GroupGridLayout.Insert(layout, inserted, new TileGroupCell(0, 1), 12);

        Assert.Equal(new TileGroupCell(0, 2), Win10GroupGridLayout.GetCell(inserted));
        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(upperLeft));
        Assert.Equal(new TileGroupCell(4, 0), Win10GroupGridLayout.GetCell(upperMiddle));
        Assert.Equal(new TileGroupCell(0, 1), Win10GroupGridLayout.GetCell(lowerLeft));
        Assert.Equal(new TileGroupCell(4, 1), Win10GroupGridLayout.GetCell(lowerMiddle));
    }

    [Fact]
    public void MovingOntoAnOccupiedRegionSwapsEqualWidthGroups()
    {
        var first = Group(0, 0);
        var second = Group(4, 0);
        var belowFirst = Group(0, 1);
        var layout = new TileLayout { Groups = [first, second, belowFirst] };

        Assert.True(Win10GroupGridLayout.Move(layout, first, new TileGroupCell(4, 0), 12));

        Assert.Equal(new TileGroupCell(4, 0), Win10GroupGridLayout.GetCell(first));
        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(second));
        Assert.Equal(new TileGroupCell(0, 1), Win10GroupGridLayout.GetCell(belowFirst));
    }

    [Fact]
    public void RemovingAGroupLeavesAReusableDesktopHole()
    {
        var upperLeft = Group(0, 0);
        var upperMiddle = Group(4, 0);
        var lowerLeft = Group(0, 1);
        var layout = new TileLayout { Groups = [upperLeft, upperMiddle, lowerLeft] };

        Assert.True(Win10GroupGridLayout.Remove(layout, upperLeft));

        Assert.Equal(new TileGroupCell(4, 0), Win10GroupGridLayout.GetCell(upperMiddle));
        Assert.Equal(new TileGroupCell(0, 1), Win10GroupGridLayout.GetCell(lowerLeft));
        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.FindAppendCell(layout, 12));
    }

    [Fact]
    public void VariableWidthGroupsOccupyTheirWholeHorizontalSpan()
    {
        var wide = Group(0, 0, widthUnits: 3);
        var narrow = Group(1, 0, widthUnits: 1);
        var layout = new TileLayout { Groups = [wide, narrow] };

        Assert.True(Win10GroupGridLayout.EnsureCoordinates(layout, 4));

        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(wide));
        Assert.Equal(new TileGroupCell(3, 0), Win10GroupGridLayout.GetCell(narrow));
    }

    private static TileGroup Group(int column, int row, int widthUnits = 4) => new()
    {
        GroupColumn = column,
        GroupRow = row,
        WidthUnits = widthUnits,
    };
}
