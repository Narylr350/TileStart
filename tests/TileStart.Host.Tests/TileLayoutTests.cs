using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileLayoutTests
{
    [Fact]
    public void ContainsLaunchTargetIncludesFolderChildrenAndNormalizedTargets()
    {
        const string executable = @"C:\Apps\Tool.exe";
        var child = new TileItem
        {
            Name = "工具",
            LaunchTarget = $@"shell:AppsFolder\{executable}",
        };
        var layout = new TileLayout
        {
            Groups =
            [
                new TileGroup
                {
                    Tiles =
                    [
                        new TileItem
                        {
                            Name = "文件夹",
                            IsTileFolder = true,
                            FolderTiles = [child],
                        },
                    ],
                },
            ],
        };

        Assert.True(layout.ContainsLaunchTarget(executable));
        Assert.False(layout.ContainsLaunchTarget(executable, child));
    }

    [Fact]
    public void EmptyLaunchTargetsDoNotBlockDistinctCustomTiles()
    {
        var first = new TileItem { Name = "命令一" };
        var second = new TileItem { Name = "命令二" };
        var layout = new TileLayout
        {
            Groups = [new TileGroup { Tiles = [first] }],
        };

        Assert.False(layout.ContainsLaunchTarget(string.Empty));
        Assert.NotEqual(TileLayout.GetIdentityKey(first), TileLayout.GetIdentityKey(second));
    }
}