namespace TileStart.Host.Tests;

public sealed class ExplorerContextMenuRegistrationTests
{
    [Fact]
    public void ContextMenuTargetsAllFilesDirectoriesAndDrives()
    {
        Assert.Equal(["*", "Directory", "Drive"], ExplorerContextMenuRegistration.RegistrationClasses);
    }

    [Fact]
    public void ExplorerCommandsUseConsistentStartTerminology()
    {
        Assert.Equal("添加到 TileStart 应用列表", ExplorerContextMenuRegistration.AddToAppListLabel);
        Assert.Equal("固定到“开始”屏幕", ExplorerContextMenuRegistration.PinToStartLabel);
        Assert.DoesNotContain("磁贴区", ExplorerContextMenuRegistration.PinToStartLabel);
    }

    [Fact]
    public void DriveCommandsDoNotQuoteTheRootPathPlaceholder()
    {
        var command = ExplorerContextMenuRegistration.BuildCommand(
            @"C:\Program Files\TileStart\TileStart.Host.exe",
            "--add-app-list",
            "Drive");

        Assert.Equal(
            "\"C:\\Program Files\\TileStart\\TileStart.Host.exe\" --add-app-list %V",
            command);
    }

    [Fact]
    public void FileAndDirectoryCommandsKeepQuotedPaths()
    {
        var command = ExplorerContextMenuRegistration.BuildCommand(
            @"C:\Program Files\TileStart\TileStart.Host.exe",
            "--add-app-list",
            "Directory");

        Assert.EndsWith("--add-app-list \"%1\"", command, StringComparison.Ordinal);
    }
}
