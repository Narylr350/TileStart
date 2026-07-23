using System.Windows;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileGroupDragTests
{
    [Fact]
    public void ResolverChoosesAWorkspaceCellWithinTheNearestShelf()
    {
        var targets = new[]
        {
            new TileGroupDropTarget(0, 0, new Rect(0, 0, 420, 232), ColumnSpan: 4),
            new TileGroupDropTarget(4, 0, new Rect(TileWorkspaceMetrics.Left(4), 0, 420, 432), ColumnSpan: 4),
            new TileGroupDropTarget(0, 1, new Rect(0, 432, 420, 232), ColumnSpan: 4),
            new TileGroupDropTarget(4, 1, new Rect(TileWorkspaceMetrics.Left(4), 432, 420, 232), ColumnSpan: 4),
        };

        Assert.Equal(new TileGroupCell(0, 0), TileGroupDropResolver.ResolveTargetCell(new Point(100, 40), targets));
        Assert.Equal(new TileGroupCell(0, 1), TileGroupDropResolver.ResolveTargetCell(new Point(100, 420), targets));
        Assert.Equal(new TileGroupCell(4, 0), TileGroupDropResolver.ResolveTargetCell(new Point(600, 40), targets));
        Assert.Equal(new TileGroupCell(4, 1), TileGroupDropResolver.ResolveTargetCell(new Point(600, 500), targets));
    }

    [Fact]
    public void PreviewCanMoveAcrossThreeWorkspaceSlotsWithoutDuplicatingGroups()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 4, 0);
        var third = Group("三", 8, 0);
        var layout = new TileLayout { Groups = [first, second, third] };
        var transaction = new TileGroupDragTransaction(layout, first, columns: 12);

        Assert.True(transaction.Preview(new TileGroupCell(8, 0)));
        Assert.Equal(new TileGroupCell(8, 0), Win10GroupGridLayout.GetCell(first));
        Assert.Equal(new TileGroupCell(0, 0), Win10GroupGridLayout.GetCell(third));
        Assert.True(transaction.Preview(new TileGroupCell(4, 0)));
        Assert.Equal(new TileGroupCell(4, 0), Win10GroupGridLayout.GetCell(first));
        Assert.Equal(new TileGroupCell(8, 0), Win10GroupGridLayout.GetCell(second));
        Assert.False(transaction.Preview(new TileGroupCell(4, 0)));
        Assert.Equal(3, layout.Groups.Distinct().Count());
        Assert.True(transaction.Commit());
    }

    [Fact]
    public void FrozenTargetsKeepAStationaryPointerStableAfterPreview()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 4, 0);
        var third = Group("三", 8, 0);
        var layout = new TileLayout { Groups = [first, second, third] };
        var transaction = new TileGroupDragTransaction(layout, first, columns: 12);
        var frozenTargets = new[]
        {
            new TileGroupDropTarget(0, 0, new Rect(0, 0, 420, 232), ColumnSpan: 4),
            new TileGroupDropTarget(4, 0, new Rect(TileWorkspaceMetrics.Left(4), 0, 420, 232), ColumnSpan: 4),
            new TileGroupDropTarget(8, 0, new Rect(TileWorkspaceMetrics.Left(8), 0, 420, 232), ColumnSpan: 4),
        };
        var stationaryPointer = new Point(TileWorkspaceMetrics.Left(8) + 210, 80);

        var firstTarget = TileGroupDropResolver.ResolveTargetCell(stationaryPointer, frozenTargets);
        Assert.True(transaction.Preview(firstTarget));

        var repeatedTarget = TileGroupDropResolver.ResolveTargetCell(stationaryPointer, frozenTargets);
        Assert.Equal(firstTarget, repeatedTarget);
        Assert.False(transaction.Preview(repeatedTarget));
    }

    [Fact]
    public void ResolverCanMoveAGroupIntoAnEmptyWorkspaceSlot()
    {
        var targets = TileGroupDropResolver.IncludeEmptyColumns(
            [
                new TileGroupDropTarget(0, 0, new Rect(0, 0, 420, 232), ColumnSpan: 4),
                new TileGroupDropTarget(4, 0, new Rect(TileWorkspaceMetrics.Left(4), 0, 420, 232), ColumnSpan: 4),
            ],
            columns: 12,
            columnSpan: 4);

        Assert.Contains(targets, target => target.Column == 8 && target.IsEmptyColumn);
        Assert.Equal(
            new TileGroupCell(8, 0),
            TileGroupDropResolver.ResolveTargetCell(new Point(TileWorkspaceMetrics.Left(8) + 210, 40), targets));
    }

    [Fact]
    public void AdjacentGroupsCanExchangeInBothDirections()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 4, 0);
        var third = Group("三", 8, 0);
        var layout = new TileLayout { Groups = [first, second, third] };

        var moveSecondRight = new TileGroupDragTransaction(layout, second, columns: 12);
        Assert.True(moveSecondRight.Preview(new TileGroupCell(8, 0)));
        Assert.True(moveSecondRight.Commit());
        Assert.Equal(new TileGroupCell(8, 0), Win10GroupGridLayout.GetCell(second));
        Assert.Equal(new TileGroupCell(4, 0), Win10GroupGridLayout.GetCell(third));

        var moveSecondLeft = new TileGroupDragTransaction(layout, second, columns: 12);
        Assert.True(moveSecondLeft.Preview(new TileGroupCell(4, 0)));
        Assert.True(moveSecondLeft.Commit());
        Assert.Equal(new TileGroupCell(4, 0), Win10GroupGridLayout.GetCell(second));
        Assert.Equal(new TileGroupCell(8, 0), Win10GroupGridLayout.GetCell(third));
    }

    [Fact]
    public void CancelRestoresOriginalCellsAfterSeveralPreviews()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 4, 0);
        var third = Group("三", 0, 1);
        var fourth = Group("四", 4, 1);
        var layout = new TileLayout { Groups = [first, second, third, fourth] };
        var original = layout.Groups.ToDictionary(group => group, Win10GroupGridLayout.GetCell);
        var transaction = new TileGroupDragTransaction(layout, third, columns: 12);

        Assert.True(transaction.Preview(new TileGroupCell(8, 0)));
        Assert.True(transaction.Preview(new TileGroupCell(0, 2)));
        Assert.True(transaction.Cancel());
        Assert.All(layout.Groups, group => Assert.Equal(original[group], Win10GroupGridLayout.GetCell(group)));
    }

    [Fact]
    public void NoOpCommitDoesNotReportLayoutChange()
    {
        var first = Group("一", 0, 0);
        var second = Group("二", 4, 0);
        var layout = new TileLayout { Groups = [first, second] };
        var transaction = new TileGroupDragTransaction(layout, first, columns: 12);

        Assert.False(transaction.Preview(new TileGroupCell(0, 0)));
        Assert.False(transaction.Commit());
    }

    private static TileGroup Group(string name, int column, int row) => new()
    {
        Name = name,
        GroupColumn = column,
        GroupRow = row,
        WidthUnits = 4,
    };
}
