using System.Windows.Media;
using TileStart.Host.Navigation;

namespace TileStart.Host.Tests;

public sealed class PopupMaterialTests
{
    [Theory]
    [InlineData(0x2B, 0x2B, 0x2B, 0xCC2B2B2B)]
    [InlineData(0x12, 0x34, 0x56, 0xCC563412)]
    public void WcaGradientUsesNativeAbgrPacking(byte red, byte green, byte blue, uint expected)
    {
        var actual = PopupMaterialManager.ComposeGradientColor(Color.FromRgb(red, green, blue), 0xCC);

        Assert.Equal(unchecked((int)expected), actual);
    }
}
