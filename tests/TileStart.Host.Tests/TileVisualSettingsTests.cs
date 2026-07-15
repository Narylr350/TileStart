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
    [InlineData(200, 128)]
    public void IconSizeIsClampedToRenderableRange(double requested, double expected)
    {
        var tile = new TileItem { IconSize = requested };

        Assert.Equal(expected, tile.IconSize);
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
        Assert.Equal("#123456", tile.BackgroundColor);
        Assert.Equal("#ABCDEF", tile.ForegroundColor);
        Assert.False(tile.ShowTitle);
        Assert.Equal(72, tile.IconSize);
        Assert.Equal(TileIconPosition.TopRight, tile.IconPosition);
    }

}
