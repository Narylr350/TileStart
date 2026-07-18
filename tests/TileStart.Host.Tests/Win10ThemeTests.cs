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
}
