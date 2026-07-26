using System.Windows.Media;
using TileStart.Host.Tiles.Settings;

namespace TileStart.Host.Tests;

public class ColorPickerMathTests
{
    [Theory]
    [InlineData("#FF0000", 0)]
    [InlineData("#FFFF00", 60)]
    [InlineData("#00FF00", 120)]
    [InlineData("#00FFFF", 180)]
    [InlineData("#0000FF", 240)]
    [InlineData("#FF00FF", 300)]
    public void ToHsvReadsHueFromPrimaries(string hex, double expectedHue)
    {
        Assert.True(ColorPickerMath.TryParseHex(hex, out var color));

        Assert.Equal(expectedHue, ColorPickerMath.ToHsv(color).Hue, 3);
    }

    [Fact]
    public void ToHsvReportsGreyAsUnsaturated()
    {
        var hsv = ColorPickerMath.ToHsv(Color.FromRgb(0x3A, 0x3A, 0x3A));

        Assert.Equal(0, hsv.Saturation, 3);
    }

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#3A3A3A")]
    [InlineData("#844E57")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void ColorSurvivesHsvRoundTrip(string hex)
    {
        Assert.True(ColorPickerMath.TryParseHex(hex, out var color));

        var restored = ColorPickerMath.ToColor(ColorPickerMath.ToHsv(color));

        Assert.Equal(hex, ColorPickerMath.ToHex(restored));
    }

    [Theory]
    [InlineData("#1F2E3D", 0x1F, 0x2E, 0x3D)]
    [InlineData("1F2E3D", 0x1F, 0x2E, 0x3D)]
    [InlineData("  #1f2e3d  ", 0x1F, 0x2E, 0x3D)]
    [InlineData("#ABC", 0xAA, 0xBB, 0xCC)]
    public void TryParseHexAcceptsSupportedForms(string text, byte red, byte green, byte blue)
    {
        Assert.True(ColorPickerMath.TryParseHex(text, out var color));

        Assert.Equal(Color.FromRgb(red, green, blue), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGHHII")]
    public void TryParseHexRejectsMalformedInput(string? text)
    {
        Assert.False(ColorPickerMath.TryParseHex(text, out _));
    }

    [Fact]
    public void HueColorIsFullySaturated()
    {
        Assert.Equal("#00FF00", ColorPickerMath.ToHex(ColorPickerMath.HueColor(120)));
    }

    [Fact]
    public void ToColorClampsValuesOutsideRange()
    {
        var overshoot = ColorPickerMath.ToColor(new ColorPickerMath.Hsv(400, 2, 2));

        // Hue wraps to 40; saturation and value clamp to 1.
        Assert.Equal("#FFAA00", ColorPickerMath.ToHex(overshoot));
    }
}
