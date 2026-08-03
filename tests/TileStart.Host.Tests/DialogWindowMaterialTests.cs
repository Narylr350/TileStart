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
    [InlineData(AppThemeStyle.Windows11, true, 2)]
    public void SystemBackdropIsLimitedToTransparentWindows11Dialogs(
        AppThemeStyle style,
        bool useMaterial,
        int expectedBackdropType)
    {
        Assert.Equal(expectedBackdropType, DialogWindowMaterialManager.ResolveBackdropType(style, useMaterial));
    }

    [Theory]
    [InlineData(0x20, 0x20, 0x20, true)]
    [InlineData(0xF3, 0xF3, 0xF3, false)]
    public void SurfaceBrightnessSelectsTheMatchingDwmCaptionMode(byte red, byte green, byte blue, bool expectedDark)
    {
        Assert.Equal(expectedDark, DialogWindowMaterialManager.IsDarkSurface(MediaColor.FromRgb(red, green, blue)));
    }
}
