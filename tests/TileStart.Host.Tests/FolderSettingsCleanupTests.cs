using System.IO;
using System.Xml.Linq;
using TileStart.Host.Tiles.Models;
using TileStart.Host.Tiles.Settings;

namespace TileStart.Host.Tests;

public sealed class FolderSettingsCleanupTests
{
    [Fact]
    public void FolderSettingsHideParentIconAndTileLayoutControls()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "HostSource",
            "Tiles",
            "Settings",
            "TileSettingsWindow.xaml.cs"));

        Assert.Contains("IconSection.Visibility = Visibility.Collapsed", source, StringComparison.Ordinal);
        Assert.Contains("TileLayoutFields.Visibility = Visibility.Collapsed", source, StringComparison.Ordinal);
        Assert.Contains("HeaderTitleText.Text = \"文件夹设置\"", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("调整大小")]
    [InlineData("更多")]
    public void FolderContextMenuHidesUnsupportedActions(string header)
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Xaml",
            "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var tileMenu = document.Descendants(presentation + "ContextMenu")
            .Single(menu => (string?)menu.Attribute(x + "Key") == "TileContextMenu");
        var menuItem = tileMenu.Descendants(presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("Header") == header);
        var folderTrigger = menuItem.Descendants(presentation + "DataTrigger")
            .Single(trigger =>
                (string?)trigger.Attribute("Binding") == "{Binding IsTileFolder}"
                && (string?)trigger.Attribute("Value") == "True");
        Assert.Contains(folderTrigger.Descendants(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Visibility"
            && (string?)setter.Attribute("Value") == "Collapsed");
    }

    [Fact]
    public void FolderCannotEnterAnUnsupportedTileSize()
    {
        var folder = new TileItem
        {
            Name = "文件夹",
            IsTileFolder = true,
            Size = TileSize.Medium,
        };
        var group = new TileGroup { Tiles = [folder] };
        var layout = new TileLayout { Groups = [group] };

        Assert.False(TileContextActions.Resize(layout, folder, TileSize.Wide));
        Assert.Equal(TileSize.Medium, folder.Size);
    }
}