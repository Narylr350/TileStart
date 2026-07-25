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
}