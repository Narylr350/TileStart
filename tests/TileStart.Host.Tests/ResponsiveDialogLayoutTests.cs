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
        Assert.Equal("Disabled", (string?)groupContentScroller.Attribute("HorizontalScrollBarVisibility"));
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

    [Fact]
    public void GroupContentEditorUsesExplicitTransferActionsInsteadOfToggleCards()
    {
        var document = LoadXaml("GroupSettingsWindow.xaml");
        var listNames = document.Descendants(Presentation + "ListBox")
            .Select(element => (string?)element.Attribute(X + "Name"))
            .ToArray();
        var buttonNames = document.Descendants(Presentation + "Button")
            .Select(element => (string?)element.Attribute(X + "Name"))
            .ToArray();

        Assert.Contains("AvailableList", listNames);
        Assert.Contains("IncludedList", listNames);
        Assert.Contains("AddButton", buttonNames);
        Assert.Contains("RemoveButton", buttonNames);
        Assert.Empty(document.Descendants(Presentation + "ToggleButton"));
    }

    [Fact]
    public void GroupContentEditorRowsHaveStableSizeIndependentOfItemNames()
    {
        var document = LoadXaml("GroupSettingsWindow.xaml");
        var itemStyle = document.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(X + "Key") == "ContentListItemStyle");
        var setters = itemStyle.Elements(Presentation + "Setter")
            .ToDictionary(
                setter => (string)setter.Attribute("Property")!,
                setter => (string?)setter.Attribute("Value"));

        Assert.Equal("58", setters["Height"]);
        Assert.Equal("Stretch", setters["HorizontalAlignment"]);
        Assert.Equal("Stretch", setters["HorizontalContentAlignment"]);

        var contentLists = document.Descendants(Presentation + "ListBox")
            .Where(element => (string?)element.Attribute(X + "Name") is "AvailableList" or "IncludedList")
            .ToArray();
        Assert.Equal(2, contentLists.Length);
        Assert.All(contentLists, list =>
            Assert.Equal("Stretch", (string?)list.Attribute("HorizontalContentAlignment")));

        Assert.Contains(document.Descendants(Presentation + "ControlTemplate").Descendants(), element =>
            element.Name.LocalName == "Win10InteractionBorder"
            && (string?)element.Attribute("CornerRadius") == "{StaticResource TileStartControlCornerRadius}");

        var contentTemplate = document.Descendants(Presentation + "DataTemplate")
            .Single(element => (string?)element.Attribute(X + "Key") == "ContentItemTemplate");
        var iconTile = contentTemplate.Descendants(Presentation + "Border").Single();
        Assert.Equal("{StaticResource TileStartDefaultTileBackgroundBrush}",
            (string?)iconTile.Attribute("Background"));
        Assert.Equal("{StaticResource TileStartTileCornerRadius}", (string?)iconTile.Attribute("CornerRadius"));

        var editorColumns = document.Descendants(Presentation + "Grid.ColumnDefinitions")
            .Select(definitions => definitions.Elements(Presentation + "ColumnDefinition").ToArray())
            .Single(columns => columns.Length == 3 && (string?)columns[1].Attribute("Width") == "104");
        Assert.Equal("*", (string?)editorColumns[0].Attribute("Width"));
        Assert.Equal("*", (string?)editorColumns[2].Attribute("Width"));
    }

    private static XDocument LoadXaml(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName);
        return XDocument.Load(path);
    }
}
