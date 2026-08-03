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
}
