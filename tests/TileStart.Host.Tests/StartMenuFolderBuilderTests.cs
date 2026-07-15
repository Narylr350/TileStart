using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class StartMenuFolderBuilderTests
{
    [Fact]
    public void BuildPreservesNestedStartMenuFoldersAndRootApplications()
    {
        var entries = StartMenuFolderBuilder.Build(
        [
            Shortcut("Root", "root.lnk", ""),
            Shortcut("Compiler", "compiler.lnk", "Developer Tools"),
            Shortcut("Profiler", "profiler.lnk", @"Developer Tools\Diagnostics"),
        ]);

        Assert.Equal(["Developer Tools", "Root"], entries.Select(entry => entry.Name));
        var folder = Assert.Single(entries, entry => entry.IsFolder);
        Assert.Equal("Compiler", folder.Children[0].Name);
        var nested = Assert.Single(folder.Children, entry => entry.IsFolder);
        Assert.Equal("Diagnostics", nested.Name);
        Assert.Equal("Profiler", Assert.Single(nested.Children).Name);
        Assert.Equal(["Compiler", "Profiler", "Root"],
                     AppEntry.FlattenApplications(entries).Select(entry => entry.Name).Order());
    }

    [Fact]
    public void BuildKeepsNewestDuplicateWithinTheSameFolder()
    {
        var old = new DateTime(2025, 1, 1);
        var newer = new DateTime(2026, 1, 1);
        var entries = StartMenuFolderBuilder.Build(
        [
            new StartMenuShortcut("Tool", "old.lnk", old, "Utilities"),
            new StartMenuShortcut("Tool", "new.lnk", newer, "Utilities"),
        ]);

        var folder = Assert.Single(entries);
        var app = Assert.Single(folder.Children);
        Assert.Equal("new.lnk", app.LaunchTarget);
        Assert.Equal(newer, app.AddedAt);
    }

    [Fact]
    public void FolderExpansionUpdatesChevron()
    {
        var folder = AppEntry.Folder("Utilities", [AppEntry.Application("Tool", "tool.lnk", DateTime.MinValue)]);

        Assert.True(folder.IsFolder);
        Assert.Equal("\uE76C", folder.FolderChevron);

        folder.IsExpanded = true;

        Assert.Equal("\uE70E", folder.FolderChevron);
    }

    private static StartMenuShortcut Shortcut(string name, string target, string folder) =>
        new(name, target, DateTime.MinValue, folder);
}
