using System.Windows.Media;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10ThemeTests
{
    [Fact]
    public void ReadDarkAccentUsesTheDarkestStartAccentPaletteEntry()
    {
        var palette = new byte[32];
        palette[24] = 0xD2;
        palette[25] = 0x41;
        palette[26] = 0x87;

        var color = Win10Theme.ReadDarkAccent(palette, Colors.Blue);

        Assert.Equal(Color.FromRgb(0xD2, 0x41, 0x87), color);
    }

    [Fact]
    public void ReadDarkAccentFallsBackWhenPaletteIsUnavailable()
    {
        Assert.Equal(Colors.Blue, Win10Theme.ReadDarkAccent(null, Colors.Blue));
        Assert.Equal(Colors.Blue, Win10Theme.ReadDarkAccent(new byte[8], Colors.Blue));
    }

    [Fact]
    public void ResolveStartMaterialUsesAcrylicWhenTransparencyIsEnabled()
    {
        var material = Win10Theme.ResolveStartMaterial(1, highContrast: false);

        Assert.True(material.UseAcrylic);
        Assert.Equal(Color.FromRgb(0x1F, 0x1F, 0x1F), material.FallbackColor);
        Assert.Equal(unchecked((int)0xBF101010), material.AcrylicGradientColor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    [InlineData("1")]
    public void ResolveStartMaterialFallsBackWhenTransparencyIsNotEnabled(object? value)
    {
        var material = Win10Theme.ResolveStartMaterial(value, highContrast: false);

        Assert.False(material.UseAcrylic);
    }

    [Fact]
    public void ResolveStartMaterialFallsBackInHighContrast()
    {
        var material = Win10Theme.ResolveStartMaterial(1, highContrast: true);

        Assert.False(material.UseAcrylic);
    }
}
