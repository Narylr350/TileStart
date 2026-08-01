using System.Windows.Media;
using TileStart.Host;
using TileStart.Host.Themes;

namespace TileStart.Host.Tests;

public sealed class Win10ThemeTests
{
    [Fact]
    public void ResolveAccentColorDecodesWindowsMenuAccentDword()
    {
        var color = Win10Theme.ResolveAccentColor(unchecked((int)0xFF6F639D), null, Colors.Blue);

        Assert.Equal(Color.FromRgb(0x9D, 0x63, 0x6F), color);
    }

    [Fact]
    public void ResolveAccentColorUsesMainPaletteEntryWhenMenuAccentIsUnavailable()
    {
        var palette = new byte[32];
        palette[12] = 0xD2;
        palette[13] = 0x41;
        palette[14] = 0x87;

        var color = Win10Theme.ResolveAccentColor(null, palette, Colors.Blue);

        Assert.Equal(Color.FromRgb(0xD2, 0x41, 0x87), color);
    }

    [Fact]
    public void ResolveAccentColorFallsBackWhenRegistryValuesAreUnavailable()
    {
        Assert.Equal(Colors.Blue, Win10Theme.ResolveAccentColor(null, null, Colors.Blue));
        Assert.Equal(Colors.Blue, Win10Theme.ResolveAccentColor("invalid", new byte[8], Colors.Blue));
    }

    [Fact]
    public void BlendCreatesStableAccentInteractionColors()
    {
        var source = Color.FromRgb(100, 120, 140);

        Assert.Equal(Color.FromRgb(116, 134, 152), Win10Theme.Blend(source, Colors.White, 0.10));
        Assert.Equal(Color.FromRgb(88, 106, 123), Win10Theme.Blend(source, Colors.Black, 0.12));
    }

    [Theory]
    [InlineData(245, 245, 245, true)]
    [InlineData(20, 20, 20, false)]
    public void AccentForegroundChoosesReadableText(byte red, byte green, byte blue, bool expectedDark)
    {
        Assert.Equal(expectedDark, Win10Theme.UseDarkForeground(Color.FromRgb(red, green, blue)));
    }

    [Fact]
    public void ResolveStartMaterialUsesAcrylicWhenTransparencyIsEnabled()
    {
        var material = Win10Theme.ResolveStartMaterial(1, highContrast: false);

        Assert.True(material.UseAcrylic);
        Assert.Equal(Color.FromRgb(0x1F, 0x1F, 0x1F), material.FallbackColor);
        Assert.Equal(unchecked((int)0xBF101010), material.AcrylicGradientColor);
    }

    [Fact]
    public void Win10StartMaterialUsesDark1AccentAcrylicWhenStartAccentIsEnabled()
    {
        var palette = new byte[32];
        palette[16] = 0x00;
        palette[17] = 0x5F;
        palette[18] = 0xBA;

        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: false,
            AppThemeStyle.Windows10,
            startColorMenu: unchecked((int)0xFFBA5F00),
            colorPrevalence: 1,
            accentPalette: palette);

        Assert.True(material.UseAcrylic);
        Assert.Equal(Color.FromRgb(0x00, 0x5F, 0xBA), material.FallbackColor);
        Assert.Equal(unchecked((int)0xB8A75704), material.AcrylicGradientColor);
    }

    [Fact]
    public void Win10AccentAcrylicFallsBackToPackedStartColorWithoutPalette()
    {
        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: false,
            AppThemeStyle.Windows10,
            startColorMenu: unchecked((int)0xFFBA5F00),
            colorPrevalence: 1,
            accentPalette: null);

        Assert.Equal(Color.FromRgb(0x00, 0x5F, 0xBA), material.FallbackColor);
        Assert.Equal(unchecked((int)0xB8A75704), material.AcrylicGradientColor);
    }

    [Fact]
    public void Win10AccentWcaTintCompensatesForMissingUwpLuminosity()
    {
        Assert.Equal(
            Color.FromRgb(0x04, 0x57, 0xA7),
            Win10Theme.ResolveWin10AccentWcaTintColor(Color.FromRgb(0x00, 0x5F, 0xBA)));
    }

    [Fact]
    public void Win10StartMaterialKeepsNormalAcrylicWhenStartAccentIsDisabled()
    {
        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: false,
            AppThemeStyle.Windows10,
            startColorMenu: unchecked((int)0xFFBA5F00),
            colorPrevalence: 0,
            accentPalette: new byte[32]);

        Assert.Equal(Color.FromRgb(0x1F, 0x1F, 0x1F), material.FallbackColor);
        Assert.Equal(unchecked((int)0xBF101010), material.AcrylicGradientColor);
    }

    [Fact]
    public void Win10AccentFallbackRemainsColoredWhenTransparencyIsDisabled()
    {
        var material = Win10Theme.ResolveStartMaterial(
            0,
            highContrast: false,
            AppThemeStyle.Windows10,
            startColorMenu: unchecked((int)0xFFBA5F00),
            colorPrevalence: 1,
            accentPalette: null);

        Assert.False(material.UseAcrylic);
        Assert.Equal(Color.FromRgb(0x00, 0x5F, 0xBA), material.FallbackColor);
    }

    [Fact]
    public void HighContrastDoesNotUseWin10AccentFallback()
    {
        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: true,
            AppThemeStyle.Windows10,
            startColorMenu: unchecked((int)0xFFBA5F00),
            colorPrevalence: 1,
            accentPalette: null);

        Assert.False(material.UseAcrylic);
        Assert.Equal(Color.FromRgb(0x1F, 0x1F, 0x1F), material.FallbackColor);
    }

    [Fact]
    public void Windows11StartMaterialKeepsWallpaperColorVisibleThroughAcrylic()
    {
        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: false,
            AppThemeStyle.Windows11);

        Assert.True(material.UseAcrylic);
        Assert.Equal(unchecked((int)0xCC1C1C1C), material.AcrylicGradientColor);
    }

    [Fact]
    public void Windows11StartMaterialTintsWithWallpaperDerivedStartColor()
    {
        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: false,
            AppThemeStyle.Windows11,
            startColorMenu: unchecked((int)0x00574E84));

        Assert.True(material.UseAcrylic);
        Assert.Equal(unchecked((int)0xBF574E84), material.AcrylicGradientColor);
    }

    [Fact]
    public void Windows11StartMaterialFallsBackToNeutralTintWithoutWallpaperColor()
    {
        Assert.Equal(
            unchecked((int)0xCC1C1C1C),
            Win10Theme.ResolveWindows11GradientColor(startColorMenu: null));
    }

    [Fact]
    public void StartSurfaceColorUnpacksWallpaperDerivedStartColor()
    {
        Assert.Equal(
            Color.FromRgb(0x84, 0x4E, 0x57),
            Win10Theme.ResolveStartSurfaceColor(unchecked((int)0x00574E84)));
    }

    [Fact]
    public void StartSurfaceColorFallsBackToNeutralWithoutWallpaperColor()
    {
        Assert.Equal(
            Color.FromRgb(0x1C, 0x1C, 0x1C),
            Win10Theme.ResolveStartSurfaceColor(startColorMenu: null));
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