namespace TileStart.Host.Tests;

public sealed class ExplorerContextMenuRegistrationTests
{
    [Fact]
    public void ContextMenuTargetsAllFilesAndDirectories()
    {
        Assert.Equal(["*", "Directory"], ExplorerContextMenuRegistration.RegistrationClasses);
    }
}
