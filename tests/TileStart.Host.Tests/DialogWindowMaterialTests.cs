using System.Windows.Media;
using TileStart.Host.Themes;
using TileStart.Host.Windowing;
using MediaColor = System.Windows.Media.Color;

namespace TileStart.Host.Tests;

public sealed class DialogWindowMaterialTests
{
    [Theory]
    [InlineData(AppThemeStyle.Windows10, true, 0)]
    [InlineData(AppThemeStyle.Windows11, false, 0)]
    [InlineData(AppThemeStyle.Windows11, true, 3)]
    public void SystemBackdropIsLimitedToTransparentWindows11Dialogs(
        AppThemeStyle style,
        bool useMaterial,
        int expectedBackdropType)
    {
        Assert.Equal(expectedBackdropType, DialogWindowMaterialManager.ResolveBackdropType(style, useMaterial));
    }

    [Theory]
    [InlineData(3, 0x20, 0x20, 0x20, unchecked((int)0xFFFFFFFE))]
    [InlineData(0, 0x12, 0x34, 0x56, 0x00563412)]
    public void AcrylicCaptionDoesNotDrawAnIndependentSolidColor(
        int backdropType,
        byte red,
        byte green,
        byte blue,
        int expected)
    {
        Assert.Equal(
            expected,
            DialogWindowMaterialManager.ResolveCaptionColor(
                backdropType,
                MediaColor.FromRgb(red, green, blue)));
    }

    [Theory]
    [InlineData(0x20, 0x20, 0x20, 0x00202020)]
    [InlineData(0x12, 0x34, 0x56, 0x00563412)]
    [InlineData(0xF3, 0xF3, 0xF3, 0x00F3F3F3)]
    public void CaptionColorUsesNativeColorRefByteOrder(byte red, byte green, byte blue, int expected)
    {
        Assert.Equal(expected, DialogWindowMaterialManager.ToColorRef(MediaColor.FromRgb(red, green, blue)));
    }
    [Theory]
    [InlineData(0x20, 0x20, 0x20, true)]
    [InlineData(0xF3, 0xF3, 0xF3, false)]
    public void SurfaceBrightnessSelectsTheMatchingDwmCaptionMode(byte red, byte green, byte blue, bool expectedDark)
    {
        Assert.Equal(expectedDark, DialogWindowMaterialManager.IsDarkSurface(MediaColor.FromRgb(red, green, blue)));
    }
}
