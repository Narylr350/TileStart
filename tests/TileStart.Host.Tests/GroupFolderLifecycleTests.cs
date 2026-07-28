using TileStart.Host.Controllers;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Tiles.Settings;

namespace TileStart.Host.Tests;

public sealed class GroupFolderLifecycleTests
{
    [Fact]
    public void FolderOptionsAreStructuralAndCannotBeRemovedFromGroupSettings()
    {
        var folder = new TileItem
        {
            Name = "工具箱",
            IsTileFolder = true,
            FolderTiles = [new TileItem { Name = "工具" }],
        };
        var option = new GroupTileOption
        {
            Key = folder.Id,
            Name = folder.Name,
            Icon = null,
            ExistingTile = folder,
            IsSelected = true,
        };

        Assert.False(option.CanRemove);
        Assert.True(new GroupTileOption
        {
            Key = "app",
            Name = "应用",
            Icon = null,
            ExistingTile = new TileItem(),
        }.CanRemove);
    }

    [Fact]
    public void SavingGroupSettingsCannotDropAnExistingFolder()
    {
        var first = new TileItem { Name = "第一个" };
        var folderChild = new TileItem { Name = "保留内容" };
        var folder = new TileItem
        {
            Name = "工具箱",
            IsTileFolder = true,
            FolderTiles = [folderChild],
        };
        var last = new TileItem { Name = "最后一个" };
        var selected = new List<TileItem> { first, last };

        TileWorkspaceController.PreserveStructuralFolders([first, folder, last], selected);

        Assert.Equal([first, folder, last], selected);
        Assert.Same(folderChild, selected[1].FolderTiles.Single());
    }

    [Fact]
    public void NewGroupFolderStartsAsAnEmptyMediumFolder()
    {
        var folder = TileWorkspaceController.CreateEmptyFolder();

        Assert.Equal("文件夹", folder.Name);
        Assert.True(folder.IsTileFolder);
        Assert.Equal(TileSize.Medium, folder.Size);
        Assert.Empty(folder.FolderTiles);
    }
}
