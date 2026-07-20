using System.Windows;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileGroupDragTests
{
    [Fact]
    public void ResolverChoosesATwoDimensionalCellWithinTheNearestColumn()
    {
        var targets = new[]
        {
            new TileGroupDropTarget(0, 0, new Rect(0, 0, 412, 232)),
            new TileGroupDropTarget(1, 0, new Rect(424, 0, 412, 432)),
            new TileGroupDropTarget(0, 1, new Rect(0, 232, 412, 232)),
            new TileGroupDropTarget(1, 1, new Rect(424, 432, 412, 232)),
        };

        Assert.Equal(new TileGroupCell(0, 0), TileGroupDropResolver.ResolveTargetCell(new Point(100, 40), targets));
        Assert.Equal(new TileGroupCell(0, 1), TileGroupDropResolver.ResolveTargetCell(new Point(100, 220), targets));
        Assert.Equal(new TileGroupCell(0, 2), TileGroupDropResolver.ResolveTargetCell(new Point(100, 700), targets));
        Assert.Equal(new TileGroupCell(1, 0), TileGroupDropResolver.ResolveTargetCell(new Point(700, 40), targets));
        Assert.Equal(new TileGroupCell(1, 1), TileGroupDropResolver.ResolveTargetCell(new Point(700, 440), targets));
        Assert.Equal(new TileGroupCell(1, 2), TileGroupDropResolver.ResolveTargetCell(new Point(700, 700), targets));
    }

    [Fact]
    public void PreviewCanMoveAcrossThreeCellsWithoutDuplicatingGroups()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 1, 0);
        var third = Group("三", 2, 0);
        var layout = new TileLayout { Groups = [first, second, third] };
        var transaction = new TileGroupDragTransaction(layout, first, columns: 3);

        Assert.True(transaction.Preview(new TileGroupCell(2, 0)));
        Assert.Equal(new TileGroupCell(2, 0), Win10GroupGridLayout.GetCell(first));
        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(third));
        Assert.True(transaction.Preview(new TileGroupCell(1, 0)));
        Assert.Equal(new TileGroupCell(1, 0), Win10GroupGridLayout.GetCell(first));
        Assert.Equal(new TileGroupCell(2, 0), Win10GroupGridLayout.GetCell(second));
        Assert.False(transaction.Preview(new TileGroupCell(1, 0)));
        Assert.Equal(3, layout.Groups.Distinct().Count());
        Assert.True(transaction.Commit());
    }

    [Fact]
    public void FrozenTargetsKeepAStationaryPointerStableAfterPreview()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 1, 0);
        var third = Group("三", 2, 0);
        var layout = new TileLayout { Groups = [first, second, third] };
        var transaction = new TileGroupDragTransaction(layout, first, columns: 3);
        var frozenTargets = new[]
        {
            new TileGroupDropTarget(0, 0, new Rect(0, 0, 412, 232)),
            new TileGroupDropTarget(1, 0, new Rect(424, 0, 412, 232)),
            new TileGroupDropTarget(2, 0, new Rect(848, 0, 412, 232)),
        };
        var stationaryPointer = new Point(1100, 180);

        var firstTarget = TileGroupDropResolver.ResolveTargetCell(stationaryPointer, frozenTargets);
        Assert.True(transaction.Preview(firstTarget));

        var repeatedTarget = TileGroupDropResolver.ResolveTargetCell(stationaryPointer, frozenTargets);
        Assert.Equal(firstTarget, repeatedTarget);
        Assert.False(transaction.Preview(repeatedTarget));
    }

    [Fact]
    public void ResolverCanMoveAGroupIntoAnEmptyThirdColumn()
    {
        var targets = TileGroupDropResolver.IncludeEmptyColumns(
            [
                new TileGroupDropTarget(0, 0, new Rect(0, 0, 412, 232)),
                new TileGroupDropTarget(1, 0, new Rect(428, 0, 412, 232)),
            ],
            columns: 3);

        Assert.Contains(targets, target => target.Column == 2 && target.IsEmptyColumn);
        Assert.Equal(
            new TileGroupCell(2, 0),
            TileGroupDropResolver.ResolveTargetCell(new Point(1062, 700), targets));
    }

    [Fact]
    public void AdjacentGroupsCanExchangeInBothDirections()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 1, 0);
        var third = Group("三", 2, 0);
        var layout = new TileLayout { Groups = [first, second, third] };

        var moveSecondRight = new TileGroupDragTransaction(layout, second, columns: 3);
        Assert.True(moveSecondRight.Preview(new TileGroupCell(2, 0)));
        Assert.True(moveSecondRight.Commit());
        Assert.Equal(new TileGroupCell(2, 0), Win10GroupGridLayout.GetCell(second));
        Assert.Equal(new TileGroupCell(1, 0), Win10GroupGridLayout.GetCell(third));

        var moveSecondLeft = new TileGroupDragTransaction(layout, second, columns: 3);
        Assert.True(moveSecondLeft.Preview(new TileGroupCell(1, 0)));
        Assert.True(moveSecondLeft.Commit());
        Assert.Equal(new TileGroupCell(1, 0), Win10GroupGridLayout.GetCell(second));
        Assert.Equal(new TileGroupCell(2, 0), Win10GroupGridLayout.GetCell(third));
    }

    [Fact]
    public void CancelRestoresOriginalCellsAfterSeveralPreviews()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 1, 0);
        var third = Group("三", 0, 1);
        var fourth = Group("四", 1, 1);
        var layout = new TileLayout { Groups = [first, second, third, fourth] };
        var original = layout.Groups.ToDictionary(group => group, Win10GroupGridLayout.GetCell);
        var transaction = new TileGroupDragTransaction(layout, third, columns: 2);

        Assert.True(transaction.Preview(new TileGroupCell(1, 0)));
        Assert.True(transaction.Preview(new TileGroupCell(0, 2)));
        Assert.True(transaction.Cancel());
        Assert.All(layout.Groups, group => Assert.Equal(original[group], Win10GroupGridLayout.GetCell(group)));
    }

    [Fact]
    public void NoOpCommitDoesNotReportLayoutChange()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 1, 0);
        var layout = new TileLayout { Groups = [first, second] };
        var transaction = new TileGroupDragTransaction(layout, first, columns: 2);

        Assert.False(transaction.Preview(new TileGroupCell(0, 0)));
        Assert.False(transaction.Commit());
    }

    private static TileGroup Group(string name, int column, int row) => new()
    {
        Name = name,
        GroupColumn = column,
        GroupRow = row,
    };
}
