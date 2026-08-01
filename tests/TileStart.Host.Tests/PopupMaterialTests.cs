using System.Windows.Media;
using TileStart.Host.Navigation;

namespace TileStart.Host.Tests;

public sealed class PopupMaterialTests
{
    [Fact]
    public void AcrylicSurfaceKeepsNonZeroWpfAlphaForNativeHitTesting()
    {
        var brush = PopupMaterialManager.CreateHitTestBackground(Color.FromRgb(0x2B, 0x2B, 0x2B));

        Assert.Equal(1, brush.Color.A);
        Assert.Equal(0x2B, brush.Color.R);
        Assert.Equal(0x2B, brush.Color.G);
        Assert.Equal(0x2B, brush.Color.B);
        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void AcrylicTransitionFadesFallbackToTheHitTestAlpha()
    {
        var animation = PopupMaterialManager.CreateMaterialTransitionAnimation(
            Color.FromRgb(0x2B, 0x2B, 0x2B),
            120);

        Assert.Equal(TimeSpan.FromMilliseconds(120), animation.Duration.TimeSpan);
        Assert.Equal(1, animation.To!.Value.A);
        Assert.Equal(0x2B, animation.To.Value.R);
        Assert.Equal(0x2B, animation.To.Value.G);
        Assert.Equal(0x2B, animation.To.Value.B);
    }

    [Theory]
    [InlineData(0x2B, 0x2B, 0x2B, 0xCC2B2B2B)]
    [InlineData(0x12, 0x34, 0x56, 0xCC563412)]
    public void WcaGradientUsesNativeAbgrPacking(byte red, byte green, byte blue, uint expected)
    {
        var actual = PopupMaterialManager.ComposeGradientColor(Color.FromRgb(red, green, blue), 0xCC);

        Assert.Equal(unchecked((int)expected), actual);
    }
}
