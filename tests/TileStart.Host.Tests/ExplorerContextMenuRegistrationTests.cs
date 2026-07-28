namespace TileStart.Host.Tests;

public sealed class ExplorerContextMenuRegistrationTests
{
    [Fact]
    public void ContextMenuTargetsAllFilesAndDirectories()
    {
        Assert.Equal(["*", "Directory"], ExplorerContextMenuRegistration.RegistrationClasses);
    }

    [Fact]
    public void ExplorerCommandsUseConsistentStartTerminology()
    {
        Assert.Equal("添加到 TileStart 应用列表", ExplorerContextMenuRegistration.AddToAppListLabel);
        Assert.Equal("固定到“开始”屏幕", ExplorerContextMenuRegistration.PinToStartLabel);
        Assert.DoesNotContain("磁贴区", ExplorerContextMenuRegistration.PinToStartLabel);
    }
}
