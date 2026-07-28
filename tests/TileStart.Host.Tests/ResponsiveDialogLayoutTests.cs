using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class ResponsiveDialogLayoutTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void SharedDialogStyleAttachesTheWorkAreaManager()
    {
        var document = LoadXaml("SharedStyles.xaml");
        var style = document.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(X + "Key") == "TileStartDialogWindowStyle");

        Assert.Contains(style.Elements(Presentation + "EventSetter"), setter =>
            (string?)setter.Attribute("Event") == "Loaded"
            && (string?)setter.Attribute("Handler") == "DialogWindow_Loaded");
    }

    [Theory]
    [InlineData("SettingsWindow.xaml")]
    [InlineData("AboutWindow.xaml")]
    [InlineData("BackupRestoreWindow.xaml")]
    [InlineData("GroupSettingsWindow.xaml")]
    [InlineData("TileSettingsWindow.xaml")]
    public void PrimaryDialogContentCanYieldSpaceWithoutClippingTheFooter(string fileName)
    {
        var document = LoadXaml(fileName);
        var rootGrid = document.Descendants(Presentation + "Border")
            .First(element => (string?)element.Attribute("Style") == "{StaticResource TileStartDialogSurfaceStyle}")
            .Element(Presentation + "Grid");
        Assert.NotNull(rootGrid);

        var rows = rootGrid!.Element(Presentation + "Grid.RowDefinitions")!
            .Elements(Presentation + "RowDefinition")
            .Select(row => (string?)row.Attribute("Height"))
            .ToArray();
        Assert.True(rows.Length >= 3);
        Assert.Equal("*", rows[1]);
        Assert.NotEqual("*", rows[^1]);
    }

    [Fact]
    public void LargeEditorsUseScrollableOrFlexibleMainContent()
    {
        var group = LoadXaml("GroupSettingsWindow.xaml");
        var groupContentScroller = group.Descendants(Presentation + "ScrollViewer")
            .Single(element => (string?)element.Attribute("Grid.Row") == "1");
        Assert.Equal("Auto", (string?)groupContentScroller.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)groupContentScroller.Attribute("VerticalScrollBarVisibility"));
        var propertyScroller = group.Descendants(Presentation + "ScrollViewer")
            .Single(element => (string?)element.Attribute(X + "Name") == "GroupPropertiesScrollViewer");
        Assert.Equal("Auto", (string?)propertyScroller.Attribute("VerticalScrollBarVisibility"));

        var tile = LoadXaml("TileSettingsWindow.xaml");
        var tileColumns = tile.Descendants(Presentation + "Grid.ColumnDefinitions")
            .SelectMany(definitions => definitions.Elements(Presentation + "ColumnDefinition"))
            .ToArray();
        Assert.Contains(tileColumns, column => (string?)column.Attribute("Width") == "2*"
            && (string?)column.Attribute("MaxWidth") == "300");
        Assert.Contains(tileColumns, column => (string?)column.Attribute("Width") == "4*"
            && (string?)column.Attribute("MinWidth") == "320");
    }

    private static XDocument LoadXaml(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName);
        return XDocument.Load(path);
    }
}
