namespace TileStart.Host.Tests;

public sealed class WindowWorkAreaLayoutTests
{
    [Fact]
    public void DialogSizeShrinksToTheLogicalWorkAreaInsteadOfClipping()
    {
        var fitted = WindowWorkAreaLayout.FitSize(
            new LogicalWindowSize(1100, 700),
            new LogicalWindowSize(620, 460),
            new LogicalWindowSize(910, 500));

        Assert.Equal(886, fitted.Width);
        Assert.Equal(476, fitted.Height);
    }

    [Fact]
    public void DialogSizeKeepsItsDesiredDimensionsWhenTheyFit()
    {
        var fitted = WindowWorkAreaLayout.FitSize(
            new LogicalWindowSize(640, 560),
            new LogicalWindowSize(420, 320),
            new LogicalWindowSize(1706, 1026));

        Assert.Equal(new LogicalWindowSize(640, 560), fitted);
    }

    [Fact]
    public void DialogCentersOverOwnerAndRemainsInsideNegativeCoordinateWorkArea()
    {
        var workArea = new PixelRect(-1920, 0, 0, 1040);
        var owner = new PixelRect(-1600, 700, -900, 1040);

        var placement = WindowWorkAreaLayout.CenterAndClamp(workArea, owner, 900, 700);

        Assert.Equal(new PixelRect(-1700, 340, -800, 1040), placement);
    }

    [Fact]
    public void OversizedDialogIsClampedToTheEntireWorkArea()
    {
        var workArea = new PixelRect(100, 50, 900, 650);

        var placement = WindowWorkAreaLayout.CenterAndClamp(workArea, null, 1200, 900);

        Assert.Equal(workArea, placement);
    }
}
