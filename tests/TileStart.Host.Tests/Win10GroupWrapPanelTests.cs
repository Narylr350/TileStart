using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10GroupWrapPanelTests
{
    [Fact]
    public void ThreeGroupsFitAtCurrentSavedTilePaneWidth()
    {
        Assert.Equal(3, Win10GroupWrapPanel.ColumnsForWidth(1267.33));
        Assert.Equal(1268, Win10GroupWrapPanel.RequiredWidth(3));
    }

    [Fact]
    public void WrapCalculationDoesNotChargeTrailingGroupGap()
    {
        Assert.Equal(2, Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(2)));
        Assert.Equal(3, Win10GroupWrapPanel.ColumnsForWidth(Win10GroupWrapPanel.RequiredWidth(3)));
    }
}
