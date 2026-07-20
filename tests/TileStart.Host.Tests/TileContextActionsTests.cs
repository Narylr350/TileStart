using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileContextActionsTests
{
    [Theory]
    [InlineData(TileSize.Small, "Small", true)]
    [InlineData(TileSize.Medium, "Small", false)]
    [InlineData(TileSize.Wide, "invalid", false)]
    [InlineData(TileSize.Large, null, false)]
    public void SelectedSizeMatchesMenuTag(TileSize current, string? tag, bool expected)
    {
        Assert.Equal(expected, TileContextActions.IsSelectedSize(current, tag));
    }

    [Theory]
    [InlineData(TileSize.Small, 48, 48)]
    [InlineData(TileSize.Medium, 100, 100)]
    [InlineData(TileSize.Wide, 204, 100)]
    [InlineData(TileSize.Large, 204, 204)]
    public void ResizeAppliesEveryWin10TileSize(TileSize size, double width, double height)
    {
        var tile = new TileItem { Size = TileSize.Medium };
        var group = new TileGroup { Tiles = [tile] };
        var layout = new TileLayout { Groups = [group] };

        var changed = TileContextActions.Resize(layout, tile, size);

        Assert.Equal(size != TileSize.Medium, changed);
        Assert.Equal(size, tile.Size);
        Assert.Equal(width, tile.PixelWidth);
        Assert.Equal(height, tile.PixelHeight);
    }

    [Fact]
    public void ResizeReflowsCollidingTiles()
    {
        var resizing = new TileItem { Size = TileSize.Medium, Column = 0, Row = 0 };
        var other = new TileItem { Size = TileSize.Medium, Column = 2, Row = 0 };
        var group = new TileGroup { Tiles = [resizing, other] };
        var layout = new TileLayout { Groups = [group] };

        Assert.True(TileContextActions.Resize(layout, resizing, TileSize.Wide));

        Assert.Equal((0, 0), (resizing.Column, resizing.Row));
        Assert.Equal((4, 0), (other.Column, other.Row));
    }

    [Fact]
    public void UnpinRemovesEmptyGroupAndKeepsNonEmptyGroup()
    {
        var only = new TileItem();
        var emptyAfterUnpin = new TileGroup { Tiles = [only] };
        var first = new TileItem();
        var remaining = new TileItem();
        var nonEmpty = new TileGroup { Tiles = [first, remaining] };
        var layout = new TileLayout { Groups = [emptyAfterUnpin, nonEmpty] };

        Assert.True(TileContextActions.Unpin(layout, only));
        Assert.DoesNotContain(emptyAfterUnpin, layout.Groups);

        Assert.True(TileContextActions.Unpin(layout, first));
        Assert.Contains(nonEmpty, layout.Groups);
        Assert.Same(remaining, Assert.Single(nonEmpty.Tiles));
    }

    [Fact]
    public void DissolveFolderRestoresEveryChildNearTheFolderOrigin()
    {
        var blocker = new TileItem { Size = TileSize.Medium, Column = 0, Row = 0 };
        var first = new TileItem { Size = TileSize.Medium, Column = 0, Row = 0 };
        var second = new TileItem { Size = TileSize.Medium, Column = 2, Row = 0 };
        var folder = new TileItem
        {
            IsTileFolder = true,
            IsFolderExpanded = true,
            Size = TileSize.Medium,
            Column = 2,
            Row = 0,
            FolderTiles = [first, second],
        };
        var group = new TileGroup { Tiles = [blocker, folder] };
        var layout = new TileLayout { Groups = [group] };

        Assert.True(TileContextActions.DissolveFolder(layout, folder));

        Assert.Equal([blocker, first, second], group.Tiles);
        Assert.Equal((2, 0), (first.Column, first.Row));
        Assert.Equal((4, 0), (second.Column, second.Row));
        Assert.Empty(folder.FolderTiles);
        Assert.False(folder.IsFolderExpanded);
        Assert.DoesNotContain(folder, group.Tiles);
    }

    [Fact]
    public void DissolveFolderRejectsAnOrdinaryTile()
    {
        var tile = new TileItem { Size = TileSize.Medium };
        var group = new TileGroup { Tiles = [tile] };
        var layout = new TileLayout { Groups = [group] };

        Assert.False(TileContextActions.DissolveFolder(layout, tile));
        Assert.Same(tile, Assert.Single(group.Tiles));
    }
}