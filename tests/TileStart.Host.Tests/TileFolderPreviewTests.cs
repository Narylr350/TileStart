using System.Collections.ObjectModel;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tests;

public sealed class TileFolderPreviewTests
{
    [Fact]
    public void PreviewUsesFirstFourTilesInVisualOrder()
    {
        var fifth = Tile("fifth", 4, 2);
        var third = Tile("third", 0, 1);
        var first = Tile("first", 0, 0);
        var fourth = Tile("fourth", 2, 1);
        var second = Tile("second", 2, 0);
        var folder = new TileItem
        {
            IsTileFolder = true,
            FolderTiles = [fifth, third, first, fourth, second],
        };

        Assert.Equal([first, second, third, fourth], folder.FolderPreviewTiles);
    }

    [Fact]
    public void PreviewRefreshesWhenContentsOrCoordinatesChange()
    {
        var first = Tile("first", 2, 0);
        var second = Tile("second", 4, 0);
        var children = new ObservableCollection<TileItem> { first, second };
        var folder = new TileItem { IsTileFolder = true, FolderTiles = children };
        var inserted = Tile("inserted", 0, 0);

        children.Add(inserted);
        Assert.Equal([inserted, first, second], folder.FolderPreviewTiles);

        second.Row = -1;
        Assert.Equal([second, inserted, first], folder.FolderPreviewTiles);

        children.Remove(second);
        Assert.Equal([inserted, first], folder.FolderPreviewTiles);
    }

    [Theory]
    [InlineData(false, "TileStart 设置…")]
    [InlineData(true, "文件夹设置…")]
    public void SettingsMenuHeaderDescribesTheTileKind(bool isFolder, string expected)
    {
        var tile = new TileItem { IsTileFolder = isFolder };

        Assert.Equal(expected, tile.SettingsMenuHeader);
    }

    private static TileItem Tile(string name, int column, int row) => new()
    {
        Name = name,
        Column = column,
        Row = row,
        Size = TileSize.Medium,
    };
}
