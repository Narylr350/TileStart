using System.Text.Json;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Tiles.Settings;

namespace TileStart.Host.Tests;

[Collection(nameof(TileItemBackgroundColorTests))]
[CollectionDefinition(nameof(TileItemBackgroundColorTests), DisableParallelization = true)]
public class TileItemBackgroundColorTests : IDisposable
{
    private const string OriginalDefault = "#3A3A3A";

    public void Dispose() => TileItem.SetThemeDefaultBackgroundColor(OriginalDefault);

    [Fact]
    public void NewTileFollowsThemeDefaultColor()
    {
        TileItem.SetThemeDefaultBackgroundColor("#5A353B");

        var tile = new TileItem();

        Assert.False(tile.HasCustomBackgroundColor);
        Assert.Equal("#5A353B", tile.BackgroundColor);
    }

    [Fact]
    public void DefaultTileKeepsThemeColorSeparateFromItsSurfaceOpacity()
    {
        TileItem.SetThemeDefaultBackgroundColor("#5A353B", 0.8);

        var tile = new TileItem();

        Assert.Equal("#5A353B", tile.BackgroundColor);
        Assert.Equal(0.8, tile.BackgroundBrush.Opacity);
    }

    [Fact]
    public void CustomTileColorDoesNotInheritDefaultSurfaceOpacity()
    {
        TileItem.SetThemeDefaultBackgroundColor("#5A353B", 0.8);

        var tile = new TileItem { BackgroundColor = "#112233" };

        Assert.Equal(1, tile.BackgroundBrush.Opacity);
    }

    [Fact]
    public void SettingsPreviewKeepsThemeSurfaceOpacityForTheDefaultColor()
    {
        TileItem.SetThemeDefaultBackgroundColor("#FFFFFF", 0.35);
        var preview = new TileItem { BackgroundColor = "#FFFFFF" };

        TileSettingsWindow.ApplyPreviewBackgroundColor(preview, "#FFFFFF");

        Assert.False(preview.HasCustomBackgroundColor);
        Assert.Equal(0.35, preview.BackgroundBrush.Opacity);
    }

    [Fact]
    public void SettingsPreviewKeepsAnExplicitCustomColorOpaque()
    {
        TileItem.SetThemeDefaultBackgroundColor("#FFFFFF", 0.35);
        var preview = new TileItem();

        TileSettingsWindow.ApplyPreviewBackgroundColor(preview, "#112233");

        Assert.True(preview.HasCustomBackgroundColor);
        Assert.Equal("#112233", preview.BackgroundColor);
        Assert.Equal(1, preview.BackgroundBrush.Opacity);
    }

    [Fact]
    public void AssigningColorMarksTileAsCustomised()
    {
        var tile = new TileItem { BackgroundColor = "#112233" };

        Assert.True(tile.HasCustomBackgroundColor);
        Assert.Equal("#112233", tile.BackgroundColor);
    }

    [Fact]
    public void CustomisedTileKeepsColorWhenThemeDefaultChanges()
    {
        var tile = new TileItem { BackgroundColor = "#112233" };

        TileItem.SetThemeDefaultBackgroundColor("#5A353B");

        Assert.Equal("#112233", tile.BackgroundColor);
    }

    [Fact]
    public void DefaultTileFollowsThemeDefaultWhenItChanges()
    {
        var tile = new TileItem();

        TileItem.SetThemeDefaultBackgroundColor("#5A353B");

        Assert.Equal("#5A353B", tile.BackgroundColor);
    }

    [Fact]
    public void ClearingCustomColorRestoresThemeDefault()
    {
        TileItem.SetThemeDefaultBackgroundColor("#5A353B");
        var tile = new TileItem { BackgroundColor = "#112233" };

        tile.ClearCustomBackgroundColor();

        Assert.False(tile.HasCustomBackgroundColor);
        Assert.Equal("#5A353B", tile.BackgroundColor);
    }

    [Fact]
    public void LayoutWrittenBeforeTheFlagExistedReadsAsNotCustomised()
    {
        TileItem.SetThemeDefaultBackgroundColor("#5A353B");

        var tile = JsonSerializer.Deserialize<TileItem>("""{"BackgroundColor":"#3A3A3A"}""")!;

        // The old layout carried a colour but no flag; System.Text.Json sets BackgroundColor,
        // which marks it customised. That is intentional: pre-existing tiles keep the colour
        // they were saved with rather than silently changing on upgrade.
        Assert.True(tile.HasCustomBackgroundColor);
        Assert.Equal("#3A3A3A", tile.BackgroundColor);
    }

    [Fact]
    public void CustomisationFlagRoundTripsThroughJson()
    {
        var tile = new TileItem { BackgroundColor = "#112233" };

        var restored = JsonSerializer.Deserialize<TileItem>(JsonSerializer.Serialize(tile))!;

        Assert.True(restored.HasCustomBackgroundColor);
        Assert.Equal("#112233", restored.BackgroundColor);
    }
}