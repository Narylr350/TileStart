using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileFolderLayoutTests
{
    [Fact]
    public void ExpandedRegionStartsAfterFolderCellAndPushesFollowingTiles()
    {
        var folder = Folder(row: 0, Child("one", TileSize.Medium, 0, 0));
        var following = Child("following", TileSize.Medium, 0, 2);
        var group = new TileGroup { Tiles = [folder, following] };

        folder.IsFolderExpanded = true;
        group.RefreshLayout();

        Assert.Equal(Win10TileMetrics.Top(2), folder.FolderRegionTop);
        Assert.Equal(following.Top + folder.FolderRegionHeight, following.DisplayTop);
        Assert.Equal(0, folder.DisplayTop);
        Assert.True(group.PixelHeight >= folder.FolderRegionTop + folder.FolderRegionHeight);
    }

    [Fact]
    public void CollapsingFolderRestoresDisplayPositionsWithoutChangingStoredRows()
    {
        var folder = Folder(row: 0, Child("one", TileSize.Medium, 0, 0));
        var following = Child("following", TileSize.Medium, 0, 2);
        var group = new TileGroup { Tiles = [folder, following] };

        folder.IsFolderExpanded = true;
        group.RefreshLayout();
        folder.IsFolderExpanded = false;
        group.RefreshLayout();

        Assert.Equal(2, following.Row);
        Assert.Equal(following.Top, following.DisplayTop);
        Assert.Equal(0, folder.FolderRegionHeight);
    }

    [Fact]
    public void MultipleExpandedRegionsAccumulateInCellOrder()
    {
        var first = Folder(row: 0, Child("first-child", TileSize.Small, 0, 0));
        var second = Folder(row: 2, Child("second-child", TileSize.Small, 0, 0));
        var following = Child("following", TileSize.Small, 0, 4);
        var group = new TileGroup { Tiles = [first, second, following] };

        first.IsFolderExpanded = true;
        second.IsFolderExpanded = true;
        group.RefreshLayout();

        Assert.Equal(Win10TileMetrics.Top(2), first.FolderRegionTop);
        Assert.Equal(Win10TileMetrics.Top(4) + first.FolderRegionHeight, second.FolderRegionTop);
        Assert.Equal(following.Top + first.FolderRegionHeight + second.FolderRegionHeight, following.DisplayTop);
    }

    [Fact]
    public void RuntimeExpansionStateIsNotSerialized()
    {
        var folder = Folder(row: 0, Child("one", TileSize.Small, 0, 0));
        folder.IsFolderExpanded = true;
        var layout = new TileLayout { Groups = [new TileGroup { Tiles = [folder] }] };

        var restored = TileLayoutStore.Deserialize(TileLayoutStore.Serialize(layout));

        Assert.False(Assert.Single(Assert.Single(restored!.Groups).Tiles).IsFolderExpanded);
    }

    private static TileItem Folder(int row, params TileItem[] children) => new()
    {
        Name = "folder",
        IsTileFolder = true,
        Size = TileSize.Medium,
        Column = 0,
        Row = row,
        FolderTiles = [.. children],
    };

    private static TileItem Child(string name, TileSize size, int column, int row) => new()
    {
        Name = name,
        Size = size,
        Column = column,
        Row = row,
    };
}
