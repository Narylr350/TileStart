using System.IO;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class DroppedTileFactoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"TileStart.Tests.{Guid.NewGuid():N}");

    public DroppedTileFactoryTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Theory]
    [InlineData("工具.lnk", TileTargetType.Application, "工具")]
    [InlineData("程序.exe", TileTargetType.Application, "程序")]
    [InlineData("启动.cmd", TileTargetType.Script, "启动")]
    [InlineData("脚本.ps1", TileTargetType.Script, "脚本")]
    [InlineData("网站.url", TileTargetType.Url, "网站")]
    [InlineData("说明.txt", TileTargetType.File, "说明.txt")]
    public void CreateClassifiesDroppedFiles(string fileName, TileTargetType expectedType, string expectedName)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, string.Empty);

        var tile = DroppedTileFactory.Create(path, _ => null);

        Assert.NotNull(tile);
        Assert.Equal(expectedType, tile.TargetType);
        Assert.Equal(expectedName, tile.Name);
        Assert.Equal(Path.GetFullPath(path), tile.LaunchTarget);
        Assert.Equal(TileSize.Medium, tile.Size);
    }

    [Fact]
    public void CreateClassifiesFolder()
    {
        var path = Directory.CreateDirectory(Path.Combine(_directory, "项目资料")).FullName;

        var tile = DroppedTileFactory.Create(path, _ => null);

        Assert.NotNull(tile);
        Assert.Equal(TileTargetType.Folder, tile.TargetType);
        Assert.Equal("项目资料", tile.Name);
    }

    [Fact]
    public void CreateRejectsMissingTarget()
    {
        Assert.Null(DroppedTileFactory.Create(Path.Combine(_directory, "missing.txt"), _ => null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
