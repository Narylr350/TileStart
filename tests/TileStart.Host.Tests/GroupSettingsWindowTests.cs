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


    [Fact]
    public void OptionsExcludeTargetsAlreadyUsedOutsideTheEditedGroup()
    {
        const string occupiedTarget = @"C:\Apps\Occupied.exe";
        const string availableTarget = @"C:\Apps\Available.exe";
        var group = new TileGroup
        {
            Tiles = [new TileItem { Name = "已有磁贴", LaunchTarget = @"C:\Apps\Existing.exe" }],
        };
        var apps = new[]
        {
            AppEntry.Application("已在其他组", occupiedTarget, DateTime.MinValue),
            AppEntry.Application("可添加", availableTarget, DateTime.MinValue),
        };
        var excludedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            LaunchTargetIdentity.GetKey(occupiedTarget),
        };

        var options = GroupSettingsWindow.CreateOptions(group, apps, excludedTargets);

        Assert.DoesNotContain(options, option => option.Name == "已在其他组");
        Assert.Contains(options, option => option.Name == "可添加" && !option.IsSelected);
        Assert.Contains(options, option => option.Name == "已有磁贴" && option.IsSelected);
    }
}