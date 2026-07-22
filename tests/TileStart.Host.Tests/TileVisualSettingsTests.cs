using System.Windows.Media;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileVisualSettingsTests
{
    [Fact]
    public void ColorsProduceBindableBrushes()
    {
        var tile = new TileItem
        {
            BackgroundColor = "#123456",
            ForegroundColor = "#ABCDEF",
        };

        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), Assert.IsType<SolidColorBrush>(tile.BackgroundBrush).Color);
        Assert.Equal(Color.FromRgb(0xAB, 0xCD, 0xEF), Assert.IsType<SolidColorBrush>(tile.ForegroundBrush).Color);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(20, 20)]
    [InlineData(200, 200)]
    [InlineData(300, 204)]
    public void IconSizeIsClampedToRenderableRange(double requested, double expected)
    {
        var tile = new TileItem { IconSize = requested };

        Assert.Equal(expected, tile.IconSize);
    }

    [Theory]
    [InlineData(TileSize.Small, 48)]
    [InlineData(TileSize.Medium, 100)]
    [InlineData(TileSize.Wide, 100)]
    [InlineData(TileSize.Large, 204)]
    public void IconSizeLimitMatchesTileShortSide(TileSize size, double expected)
    {
        Assert.Equal(expected, Win10TileMetrics.MaxIconSize(size));
    }

    [Fact]
    public void IconSizeScalesWithTileSizeAndRoundTrips()
    {
        var large = Win10TileMetrics.ScaleIconSize(96, TileSize.Medium, TileSize.Large);
        var medium = Win10TileMetrics.ScaleIconSize(large, TileSize.Large, TileSize.Medium);

        Assert.Equal(196, large);
        Assert.Equal(96, medium);
    }

    [Fact]
    public void InvalidPersistedColorsFallBackWithoutThrowing()
    {
        var tile = new TileItem
        {
            BackgroundColor = "not-a-color",
            ForegroundColor = "also-invalid",
        };

        Assert.Equal(Color.FromRgb(0x3A, 0x3A, 0x3A), Assert.IsType<SolidColorBrush>(tile.BackgroundBrush).Color);
        Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(tile.ForegroundBrush).Color);
    }

    [Fact]
    public void LayoutJsonRoundTripPreservesVisualSettings()
    {
        var layout = new TileLayout
        {
            Groups =
            [
                new TileGroup
                {
                    Tiles =
                    [
                        new TileItem
                        {
                            Name = "终端",
                            Subtitle = "开发工具",
                            BackgroundImagePath = @"C:\Images\terminal.png",
                            IconPath = @"C:\Images\terminal.svg",
                            IconSourceKind = CustomIconSourceKind.Svg,
                            IconSourceValue = @"C:\Images\terminal.svg",
                            BackgroundColor = "#123456",
                            ForegroundColor = "#ABCDEF",
                            ShowTitle = false,
                            IconSize = 72,
                            IconPosition = TileIconPosition.TopRight,
                        },
                    ],
                },
            ],
        };

        var restored = TileLayoutStore.Deserialize(TileLayoutStore.Serialize(layout));

        var tile = Assert.Single(Assert.Single(restored!.Groups).Tiles);
        Assert.Equal("开发工具", tile.Subtitle);
        Assert.Equal(@"C:\Images\terminal.png", tile.BackgroundImagePath);
        Assert.Equal(@"C:\Images\terminal.svg", tile.IconPath);
        Assert.Equal(CustomIconSourceKind.Svg, tile.IconSourceKind);
        Assert.Equal(@"C:\Images\terminal.svg", tile.IconSourceValue);
        Assert.Equal("#123456", tile.BackgroundColor);
        Assert.Equal("#ABCDEF", tile.ForegroundColor);
        Assert.False(tile.ShowTitle);
        Assert.Equal(72, tile.IconSize);
        Assert.Equal(TileIconPosition.TopRight, tile.IconPosition);
    }

}
