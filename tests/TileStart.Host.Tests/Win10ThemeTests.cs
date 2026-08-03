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
    public void Win10StartMaterialDerivesWin10Dark1FromMainAccentInsteadOfHostDark1()
    {
        var palette = new byte[32];
        palette[12] = 0x9E;
        palette[13] = 0x64;
        palette[14] = 0x70;
        palette[16] = 0x84;
        palette[17] = 0x4E;
        palette[18] = 0x57;

        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: false,
            AppThemeStyle.Windows10,
            startColorMenu: unchecked((int)0x00574E84),
            colorPrevalence: 1,
            accentPalette: palette);

        Assert.True(material.UseAcrylic);
        Assert.Equal(Color.FromRgb(0x8E, 0x5A, 0x65), material.FallbackColor);
        Assert.NotEqual(Color.FromRgb(0x84, 0x4E, 0x57), material.FallbackColor);
        Assert.Equal(unchecked((int)0xB8605686), material.AcrylicGradientColor);
    }

    [Fact]
    public void Win10AccentAcrylicPrefersAccentColorMenuWhenAvailable()
    {
        var material = Win10Theme.ResolveStartMaterial(
            1,
            highContrast: false,
            AppThemeStyle.Windows10,
            startColorMenu: unchecked((int)0x00574E84),
            colorPrevalence: 1,
            accentPalette: null,
            accentColorMenu: unchecked((int)0x0070649E));

        Assert.Equal(Color.FromRgb(0x8E, 0x5A, 0x65), material.FallbackColor);
        Assert.Equal(unchecked((int)0xB8605686), material.AcrylicGradientColor);
    }

    [Fact]
    public void Win10AccentWcaTintCompensatesForMissingUwpLuminosity()
    {
        Assert.Equal(
            Color.FromRgb(0x86, 0x56, 0x60),
            Win10Theme.ResolveWin10AccentWcaTintColor(Color.FromRgb(0x8E, 0x5A, 0x65)));
    }

    [Fact]
    public void Win10Dark1ApproximationMatchesCapturedWin10PaletteWithinOneChannelValue()
    {
        var derived = Win10Theme.DeriveWin10Dark1(Color.FromRgb(0x9E, 0x64, 0x70));
        var captured = Color.FromRgb(0x8F, 0x59, 0x64);

        Assert.Equal(Color.FromRgb(0x8E, 0x5A, 0x65), derived);
        Assert.InRange(Math.Abs(derived.R - captured.R), 0, 1);
        Assert.InRange(Math.Abs(derived.G - captured.G), 0, 1);
        Assert.InRange(Math.Abs(derived.B - captured.B), 0, 1);
    }

    [Theory]
    [InlineData(0x00, 0x00, 0x00, 0x00, 0x00, 0x00)]
    [InlineData(0xFF, 0xFF, 0xFF, 0xE6, 0xE6, 0xE6)]
    [InlineData(0x80, 0x80, 0x80, 0x73, 0x73, 0x73)]
    [InlineData(0xFF, 0x00, 0x00, 0xE6, 0x00, 0x00)]
    [InlineData(0x00, 0xFF, 0x00, 0x00, 0xE6, 0x00)]
    [InlineData(0x00, 0x00, 0xFF, 0x00, 0x00, 0xE6)]
    public void Win10Dark1ApproximationUsesTheSameRuleForRepresentativeAccents(
        byte red,
        byte green,
        byte blue,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        Assert.Equal(
            Color.FromRgb(expectedRed, expectedGreen, expectedBlue),
            Win10Theme.DeriveWin10Dark1(Color.FromRgb(red, green, blue)));
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