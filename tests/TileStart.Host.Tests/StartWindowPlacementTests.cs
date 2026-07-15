using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class StartWindowPlacementTests
{
    private static readonly PixelRect Monitor = new(0, 0, 2560, 1600);

    [Theory]
    [InlineData(0, 0, 2560, 60, TaskbarEdge.Top)]
    [InlineData(0, 1540, 2560, 1600, TaskbarEdge.Bottom)]
    [InlineData(0, 0, 60, 1600, TaskbarEdge.Left)]
    [InlineData(2500, 0, 2560, 1600, TaskbarEdge.Right)]
    [InlineData(0, 1598, 2560, 1600, TaskbarEdge.Bottom)]
    public void InferTaskbarEdgeUsesActualTaskbarWindow(
        int left,
        int top,
        int right,
        int bottom,
        TaskbarEdge expected)
    {
        Assert.Equal(expected, StartWindowPlacement.InferTaskbarEdge(Monitor, new PixelRect(left, top, right, bottom)));
    }

    [Fact]
    public void MissingTaskbarFallsBackToBottomEdge()
    {
        Assert.Equal(TaskbarEdge.Bottom, StartWindowPlacement.InferTaskbarEdge(Monitor, null));
    }

    [Theory]
    [InlineData(TaskbarEdge.Bottom, 0, 388)]
    [InlineData(TaskbarEdge.Top, 0, 60)]
    [InlineData(TaskbarEdge.Left, 60, 0)]
    [InlineData(TaskbarEdge.Right, 622, 0)]
    public void CalculateAnchorsNextToEachTaskbarEdge(TaskbarEdge edge, int expectedLeft, int expectedTop)
    {
        var workArea = edge switch
        {
            TaskbarEdge.Top => new PixelRect(0, 60, 2560, 1600),
            TaskbarEdge.Bottom => new PixelRect(0, 0, 2560, 1540),
            TaskbarEdge.Left => new PixelRect(60, 0, 2560, 1600),
            TaskbarEdge.Right => new PixelRect(0, 0, 2500, 1600),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };

        var placement = StartWindowPlacement.Calculate(workArea, edge, 1878, 1152);

        Assert.Equal(new PixelRect(expectedLeft, expectedTop, expectedLeft + 1878, expectedTop + 1152), placement);
    }

    [Fact]
    public void CalculateClampsWindowToSmallWorkArea()
    {
        var placement = StartWindowPlacement.Calculate(new PixelRect(100, 50, 900, 650), TaskbarEdge.Bottom, 1200, 900);

        Assert.Equal(new PixelRect(100, 50, 900, 650), placement);
    }
}
