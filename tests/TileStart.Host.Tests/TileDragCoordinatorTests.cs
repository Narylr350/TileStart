using System.Windows;
using TileStart.Host;
using TileStart.Host.Controllers;

namespace TileStart.Host.Tests;

public sealed class TileDragCoordinatorTests
{
    [Fact]
    public void AppDragAnchorKeepsTheSameRelativeGrabPointWhenItBecomesATile()
    {
        var anchor = TileDragCoordinator.ScaleAnchor(
            new Point(225, 36),
            sourceWidth: 300,
            sourceHeight: 48,
            targetWidth: 100,
            targetHeight: 100);

        Assert.Equal(new Point(75, 75), anchor);
    }
}