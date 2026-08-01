using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class TileFolderAppearanceTests
{
    private static readonly string MainWindowXaml = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Xaml",
        "MainWindow.xaml");

    private static readonly string SharedStylesXaml = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Xaml",
        "SharedStyles.xaml");

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void CollapsedFolderUsesThreeByThreeIconSlotsAndRegularTileBranding()
    {
        var visual = NamedElement("FolderVisual");
        var preview = NamedElement("FolderPreview");
        var previewPanel = Assert.Single(preview.Descendants(), element => element.Name.LocalName == "UniformGrid");

        Assert.Equal("0", visual.Attribute("Grid.Row")?.Value);
        Assert.Equal("2", visual.Attribute("Grid.RowSpan")?.Value);
        Assert.Null(preview.Attribute("Grid.Row"));
        Assert.Equal("{Binding PixelWidth}", preview.Attribute("Width")?.Value);
        Assert.Equal("{Binding PixelHeight}", preview.Attribute("Height")?.Value);
        Assert.Equal("3", previewPanel.Attribute("Rows")?.Value);
        Assert.Equal("3", previewPanel.Attribute("Columns")?.Value);
        Assert.Equal("{Binding Name}", NamedElement("TileTitle").Attribute("Text")?.Value);
        Assert.DoesNotContain(preview.Descendants(), element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Background")?.Value == "{Binding BackgroundBrush}");
    }

    [Fact]
    public void FolderStateTriggersSwapThePreviewForTheFullTileCollapseGlyph()
    {
        var visual = NamedElement("FolderVisual");
        var collapseGlyph = NamedElement("FolderCollapseGlyph");
        var template = visual.Ancestors().First(element => element.Name.LocalName == "DataTemplate");

        Assert.Equal("Collapsed", visual.Attribute("Visibility")?.Value);
        Assert.Equal("Collapsed", collapseGlyph.Attribute("Visibility")?.Value);
        Assert.Null(collapseGlyph.Attribute("Grid.Row"));
        Assert.Null(collapseGlyph.Attribute("Background"));
        Assert.Contains(template.Descendants(), element => IsSetter(element, "FolderVisual", "Visibility", "Visible"));
        var folderTrigger = Assert.Single(template.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && element.Attribute("Binding")?.Value == "{Binding IsTileFolder}"
            && element.Attribute("Value")?.Value == "True");
        Assert.DoesNotContain(folderTrigger.Descendants(),
            element => IsSetter(element, "TileBranding", "Visibility", "Collapsed"));
        Assert.Contains(template.Descendants(),
            element => IsSetter(element, "FolderPreview", "Visibility", "Collapsed"));
        Assert.Contains(template.Descendants(),
            element => IsSetter(element, "FolderCollapseGlyph", "Visibility", "Visible"));
    }

    [Fact]
    public void ExpandedFolderRegionIsInlineTransparentAndUnframed()
    {
        var region = NamedElement("FolderRegion");
        var layoutGrid = Assert.Single(region.Elements(), element => element.Name.LocalName == "Grid");
        var rowHeights = layoutGrid
            .Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements()
            .Select(element => element.Attribute("Height")?.Value ?? string.Empty)
            .ToArray();

        Assert.Equal("Transparent", region.Attribute("Background")?.Value);
        Assert.Equal(["*", "4"], rowHeights);
        Assert.DoesNotContain(layoutGrid.Elements(), element => element.Name.LocalName == "Border");
        Assert.DoesNotContain(XDocument.Load(MainWindowXaml).Descendants(), element =>
            element.Attribute(Xaml + "Name")?.Value == "FolderNameTextBox");
    }

    [Fact]
    public void ExpandedFolderTilesReuseTheRegularTileVisualHierarchy()
    {
        var branding = NamedElement("FolderChildBranding");
        var icon = NamedElement("FolderChildIcon");
        var animatedIcon = NamedElement("FolderChildAnimatedIcon");

        Assert.Equal("{StaticResource TileStartTileTitleContainerStyle}", branding.Attribute("Style")?.Value);
        var titleContainerStyle = Assert.Single(
            XDocument.Load(SharedStylesXaml).Descendants(),
            element => element.Name.LocalName == "Style"
                       && element.Attribute(Xaml + "Key")?.Value == "TileStartTileTitleContainerStyle");
        Assert.Contains(titleContainerStyle.Elements(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Grid.Row"
            && element.Attribute("Value")?.Value == "1");
        Assert.Contains(titleContainerStyle.Elements(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "VerticalAlignment"
            && element.Attribute("Value")?.Value == "Bottom");
        Assert.Contains(titleContainerStyle.Elements(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Margin"
            && element.Attribute("Value")?.Value.Contains("TileBrandingMargin", StringComparison.Ordinal) == true);
        Assert.NotNull(icon.Descendants().SingleOrDefault(element => element.Name.LocalName == "MultiBinding"));
        Assert.Contains("IconPath", animatedIcon.ToString(), StringComparison.Ordinal);
    }

    private static XElement NamedElement(string name)
    {
        var document = XDocument.Load(MainWindowXaml);
        return Assert.Single(document.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == name);
    }

    private static bool IsSetter(XElement element, string targetName, string property, string value) =>
        element.Name.LocalName == "Setter"
        && element.Attribute("TargetName")?.Value == targetName
        && element.Attribute("Property")?.Value == property
        && element.Attribute("Value")?.Value == value;
}
