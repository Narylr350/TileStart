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
        Assert.Contains(style.Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Opacity"
            && (string?)setter.Attribute("Value") == "0");

        var codeBehind = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "HostSource",
            "Themes",
            "SharedStyles.xaml.cs"));
        Assert.True(
            codeBehind.IndexOf("DialogWindowMaterialManager.Apply(window);", StringComparison.Ordinal)
            < codeBehind.IndexOf("DialogWindowMotion.Open(window);", StringComparison.Ordinal));
    }

    [Fact]
    public void SharedToolTipsUseThemeAwareSurfaceAndCornerRadius()
    {
        var document = LoadXaml("SharedStyles.xaml");
        var style = document.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute("TargetType") == "ToolTip"
                               && element.Attribute(X + "Key") is null);
        var border = style.Descendants(Presentation + "ControlTemplate")
            .Single()
            .Element(Presentation + "Border");

        Assert.Equal("{TemplateBinding Background}", (string?)border?.Attribute("Background"));
        Assert.Equal("{DynamicResource TileStartOverlayCornerRadius}",
            (string?)border?.Attribute("CornerRadius"));
    }

    [Fact]
    public void NavigationRevealBackgroundUsesThemeAwareOverlayRadius()
    {
        var document = LoadXaml("MainWindow.xaml");
        var railStyle = document.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(X + "Key") == "RailButtonStyle");
        var interactionBorder = railStyle.Descendants()
            .Single(element => element.Name.LocalName == "Win10InteractionBorder");

        Assert.Equal("{DynamicResource TileStartOverlayCornerRadius}",
            (string?)interactionBorder.Attribute("CornerRadius"));
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
    public void TilePaneBlankAreaExposesFolderCreation()
    {
        var document = LoadXaml("MainWindow.xaml");
        var createFolderItems = document.Descendants(Presentation + "MenuItem")
            .Where(item => (string?)item.Attribute("Click") == "AddFolder_Click")
            .ToArray();

        var createFolderItem = Assert.Single(createFolderItems);
        Assert.Equal("新建文件夹…", (string?)createFolderItem.Attribute("Header"));
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
        {
            Assert.Equal("Stretch", (string?)list.Attribute("HorizontalContentAlignment"));
            Assert.Equal("4", (string?)list.Attribute("Grid.Row"));
        });

        var namedElements = document.Descendants()
            .Select(element => (string?)element.Attribute(X + "Name"))
            .Where(name => name != null)
            .ToArray();
        Assert.DoesNotContain("DialogDescriptionText", namedElements);
        Assert.DoesNotContain("ContentEditorTitleText", namedElements);
        Assert.DoesNotContain("ContentEditorDescriptionText", namedElements);

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
            .Single(columns => columns.Length == 3 && (string?)columns[1].Attribute("Width") == "56");
        Assert.Equal("*", (string?)editorColumns[0].Attribute("Width"));
        Assert.Equal("*", (string?)editorColumns[2].Attribute("Width"));

        var propertiesColumn = document.Descendants(Presentation + "ColumnDefinition")
            .Single(column => (string?)column.Attribute(X + "Name") == "GroupPropertiesColumn");
        Assert.Equal("250", (string?)propertiesColumn.Attribute("Width"));

        var transferStyle = document.Descendants(Presentation + "Style")
            .Single(element => (string?)element.Attribute(X + "Key") == "TransferButtonStyle");
        var transferSetters = transferStyle.Elements(Presentation + "Setter")
            .ToDictionary(
                setter => (string)setter.Attribute("Property")!,
                setter => (string?)setter.Attribute("Value"));
        Assert.Equal("40", transferSetters["Width"]);
        Assert.Equal("0", transferSetters["MinWidth"]);
        Assert.Equal("40", transferSetters["Height"]);
        Assert.Equal("Center", transferSetters["HorizontalContentAlignment"]);
        Assert.Equal("Center", transferSetters["VerticalContentAlignment"]);
        var transferTemplateBorder = transferStyle
            .Descendants(Presentation + "ControlTemplate")
            .Single()
            .Element(Presentation + "Border");
        Assert.Equal("{DynamicResource TileStartOverlayCornerRadius}",
            (string?)transferTemplateBorder?.Attribute("CornerRadius"));
    }

    private static XDocument LoadXaml(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName);
        return XDocument.Load(path);
    }
}