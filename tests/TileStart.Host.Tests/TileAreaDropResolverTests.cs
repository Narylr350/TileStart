using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileAreaDropResolverTests
{
    [Fact]
    public void BlankSpaceBelowAGroupStillTargetsThatGroupColumn()
    {
        var first = new TileGroupDropZone("first", 0, 0, 412, 100);
        var second = new TileGroupDropZone("second", 428, 0, 412, 300);

        var target = TileAreaDropResolver.FindTarget([first, second], 206, 260);

        Assert.Equal("first", target?.GroupId);
    }

    [Fact]
    public void SpaceOutsideExistingGroupColumnsCreatesNoGroupTarget()
    {
        var group = new TileGroupDropZone("first", 0, 0, 412, 100);

        Assert.Null(TileAreaDropResolver.FindTarget([group], 420, 260));
    }

    [Fact]
    public void WrappedRowsChooseTheNearestGroupAtThatHorizontalColumn()
    {
        var upper = new TileGroupDropZone("upper", 0, 0, 412, 100);
        var lower = new TileGroupDropZone("lower", 0, 400, 412, 100);

        Assert.Equal("lower", TileAreaDropResolver.FindTarget([upper, lower], 200, 430)?.GroupId);
    }
    [Fact]
    public void PointerInsideThirdExistingGroupTargetsThatGroup()
    {
        var first = new TileGroupDropZone("first", 0, 0, 412, 200);
        var second = new TileGroupDropZone("second", 428, 0, 412, 200);
        var third = new TileGroupDropZone("third", 856, 0, 412, 200);

        Assert.Equal("third", TileAreaDropResolver.FindTarget([first, second, third], 900, 100)?.GroupId);
    }

    [Theory]
    [InlineData(412, "first")]
    [InlineData(420, "second")]
    [InlineData(427.9, "second")]
    public void GapBetweenGroupsBelongsToAnAdjacentGroup(double pointerX, string expected)
    {
        var first = new TileGroupDropZone("first", 0, 0, 412, 200);
        var second = new TileGroupDropZone("second", 428, 0, 412, 200);

        Assert.Equal(expected, TileAreaDropResolver.FindTarget([first, second], pointerX, 100)?.GroupId);
    }

}
