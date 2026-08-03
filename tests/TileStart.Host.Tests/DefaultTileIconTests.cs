using TileStart.Host.Controllers;
using TileStart.Host.Icons;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tests;

public sealed class DefaultTileIconTests
{
    [Fact]
    public void CommandTilesUseTheTileStartApplicationIconAsTheirFallback()
    {
        var tile = new TileItem { TargetType = TileTargetType.Command };

        Assert.Same(TileStartAppIcon.Image, ApplicationPaneController.ResolveFallbackIcon(tile));
    }

    [Fact]
    public void MissingThirdPartyApplicationIconsKeepTheGenericFallback()
    {
        var tile = new TileItem { TargetType = TileTargetType.Application };

        Assert.Same(GenericAppIcon.Image, ApplicationPaneController.ResolveFallbackIcon(tile));
    }

    [Fact]
    public void SettingsReuseAnAlreadyLoadedDefaultIcon()
    {
        var tile = new TileItem
        {
            Icon = GenericAppIcon.Image,
            IconSourceKind = CustomIconSourceKind.Default,
        };

        Assert.True(TileWorkspaceController.CanReuseLoadedDefaultIcon(tile));
    }

    [Theory]
    [InlineData(CustomIconSourceKind.LocalFile, "custom.png")]
    [InlineData(CustomIconSourceKind.Network, "cached.png")]
    [InlineData(CustomIconSourceKind.Svg, "cached.png")]
    public void SettingsRebuildTheDefaultIconWhenTheTileUsesACustomSource(
        CustomIconSourceKind sourceKind,
        string iconPath)
    {
        var tile = new TileItem
        {
            Icon = GenericAppIcon.Image,
            IconSourceKind = sourceKind,
            IconPath = iconPath,
        };

        Assert.False(TileWorkspaceController.CanReuseLoadedDefaultIcon(tile));
    }
}
