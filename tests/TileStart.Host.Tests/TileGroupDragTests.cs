using System.Windows;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileGroupDragTests
{
    [Fact]
    public void ResolverChoosesNearestPackedSlotWithinColumn()
    {
        var targets = new[]
        {
            new TileGroupDropTarget(0, new Rect(0, 0, 412, 232)),
            new TileGroupDropTarget(1, new Rect(424, 0, 412, 432)),
            new TileGroupDropTarget(2, new Rect(0, 256, 412, 232)),
            new TileGroupDropTarget(3, new Rect(424, 456, 412, 232)),
        };

        Assert.Equal(0, TileGroupDropResolver.ResolveTargetIndex(new Point(100, 40), targets));
        Assert.Equal(0, TileGroupDropResolver.ResolveTargetIndex(new Point(100, 220), targets));
        Assert.Equal(2, TileGroupDropResolver.ResolveTargetIndex(new Point(100, 300), targets));
        Assert.Equal(2, TileGroupDropResolver.ResolveTargetIndex(new Point(100, 440), targets));
        Assert.Equal(1, TileGroupDropResolver.ResolveTargetIndex(new Point(700, 40), targets));
        Assert.Equal(1, TileGroupDropResolver.ResolveTargetIndex(new Point(700, 300), targets));
        Assert.Equal(3, TileGroupDropResolver.ResolveTargetIndex(new Point(700, 440), targets));
        Assert.Equal(3, TileGroupDropResolver.ResolveTargetIndex(new Point(700, 700), targets));
    }

    [Fact]
    public void PreviewCanMoveAcrossThreeSlotsWithoutDuplicatingGroups()
    {
        var first = new TileGroup { Name = "一" };
        var second = new TileGroup { Name = "二" };
        var third = new TileGroup { Name = "三" };
        var layout = new TileLayout { Groups = [first, second, third] };
        var transaction = new TileGroupDragTransaction(layout, first);

        Assert.True(transaction.Preview(2));
        Assert.Equal([second, third, first], layout.Groups);
        Assert.True(transaction.Preview(1));
        Assert.Equal([second, first, third], layout.Groups);
        Assert.False(transaction.Preview(1));
        Assert.Equal(3, layout.Groups.Distinct().Count());
        Assert.True(transaction.Commit());
    }

    [Fact]
    public void FrozenTargetsKeepAStationaryPointerStableAfterPreviewReorder()
    {
        var first = new TileGroup { Name = "一" };
        var second = new TileGroup { Name = "二" };
        var third = new TileGroup { Name = "三" };
        var layout = new TileLayout { Groups = [first, second, third] };
        var transaction = new TileGroupDragTransaction(layout, first);
        var frozenTargets = new[]
        {
            new TileGroupDropTarget(0, new Rect(0, 0, 412, 232)),
            new TileGroupDropTarget(1, new Rect(424, 0, 412, 232)),
            new TileGroupDropTarget(2, new Rect(848, 0, 412, 232)),
        };
        var stationaryPointer = new Point(1100, 180);

        var firstTarget = TileGroupDropResolver.ResolveTargetIndex(stationaryPointer, frozenTargets);
        Assert.True(transaction.Preview(firstTarget));
        Assert.Equal([second, third, first], layout.Groups);

        var repeatedTarget = TileGroupDropResolver.ResolveTargetIndex(stationaryPointer, frozenTargets);
        Assert.Equal(firstTarget, repeatedTarget);
        Assert.False(transaction.Preview(repeatedTarget));
        Assert.Equal([second, third, first], layout.Groups);
    }

    [Fact]
    public void AdjacentGroupsCanExchangeInBothDirections()
    {
        var first = new TileGroup { Name = "一" };
        var second = new TileGroup { Name = "二" };
        var third = new TileGroup { Name = "三" };
        var layout = new TileLayout { Groups = [first, second, third] };

        var moveSecondRight = new TileGroupDragTransaction(layout, second);
        Assert.True(moveSecondRight.Preview(2));
        Assert.Equal([first, third, second], layout.Groups);
        Assert.True(moveSecondRight.Commit());

        var moveSecondLeft = new TileGroupDragTransaction(layout, second);
        Assert.True(moveSecondLeft.Preview(1));
        Assert.Equal([first, second, third], layout.Groups);
        Assert.True(moveSecondLeft.Commit());
    }

    [Fact]
    public void CancelRestoresOriginalOrderAfterSeveralPreviews()
    {
        var first = new TileGroup { Name = "一" };
        var second = new TileGroup { Name = "二" };
        var third = new TileGroup { Name = "三" };
        var fourth = new TileGroup { Name = "四" };
        var layout = new TileLayout { Groups = [first, second, third, fourth] };
        var transaction = new TileGroupDragTransaction(layout, third);

        Assert.True(transaction.Preview(0));
        Assert.True(transaction.Preview(3));
        Assert.True(transaction.Cancel());
        Assert.Equal([first, second, third, fourth], layout.Groups);
    }

    [Fact]
    public void NoOpCommitDoesNotReportLayoutChange()
    {
        var first = new TileGroup();
        var second = new TileGroup();
        var layout = new TileLayout { Groups = [first, second] };
        var transaction = new TileGroupDragTransaction(layout, first);

        Assert.False(transaction.Preview(0));
        Assert.False(transaction.Commit());
    }
}