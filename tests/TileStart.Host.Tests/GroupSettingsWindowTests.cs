using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class GroupSettingsWindowTests
{
    [Fact]
    public void ExistingTileExcludesTheSameClassicAppWithAnAppsFolderPrefix()
    {
        const string executable = @"C:\Apps\BaiduNetdisk\BaiduNetdisk.exe";
        var group = new TileGroup
        {
            Tiles =
            [
                new TileItem
                {
                    Name = "百度网盘",
                    LaunchTarget = $@"shell:AppsFolder\{executable}",
                },
            ],
        };
        var apps = new[]
        {
            AppEntry.Application("百度网盘", executable, DateTime.MinValue),
            AppEntry.Application("其他应用", @"C:\Apps\Other.exe", DateTime.MinValue),
        };

        var options = GroupSettingsWindow.CreateOptions(group, apps);

        Assert.Single(options, option => option.Name == "百度网盘");
        Assert.Contains(options, option => option.Name == "其他应用");
    }
    [Fact]
    public void FolderModeExcludesNestedFoldersAndReusesApplicationOptions()
    {
        const string executable = @"C:\Apps\Tool.exe";
        var existingApp = new TileItem { Name = "工具", LaunchTarget = executable };
        var custom = new TileItem { Name = "自定义命令", LaunchTarget = "custom:command" };
        var nestedFolder = new TileItem { Name = "嵌套", IsTileFolder = true };
        var folder = new TileItem
        {
            Name = "工具箱",
            IsTileFolder = true,
            FolderTiles = [existingApp, custom, nestedFolder],
        };
        var apps = new[]
        {
            AppEntry.Application("工具", executable, DateTime.MinValue),
            AppEntry.Application("其他应用", @"C:\Apps\Other.exe", DateTime.MinValue),
        };

        var previewGroup = GroupSettingsWindow.CreateFolderContentsGroup(folder);
        var options = GroupSettingsWindow.CreateOptions(previewGroup, apps);

        Assert.Equal(Win10TileMetrics.GroupColumns, previewGroup.WidthUnits);
        Assert.DoesNotContain(previewGroup.Tiles, tile => tile.IsTileFolder);
        Assert.Contains(options, option => option.ExistingTile == existingApp && option.IsSelected);
        Assert.Contains(options, option => option.ExistingTile == custom && option.IsSelected);
        Assert.Single(options, option => option.Name == "工具");
        Assert.Contains(options, option => option.Name == "其他应用" && !option.IsSelected);
    }

}