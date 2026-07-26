using System.Drawing;
using System.IO;
using System.Xml.Linq;
using TileStart.Host.Themes;

namespace TileStart.Host.Tests;

public sealed class DarkThemeVisualTests
{
    [Fact]
    public void MainWindowUsesTheSharedPrimaryTextBrushForTextBlocks()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = document.Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute("TargetType") == "TextBlock"
                && element.Attribute(x + "Key") is null);

        Assert.Contains(
            style.Elements(presentation + "Setter"),
            setter =>
                (string?)setter.Attribute("Property") == "Foreground"
                && (string?)setter.Attribute("Value") == "{StaticResource TileStartTextPrimaryBrush}");

        Assert.Equal("#FFFFFFFF", ReadThemeBrushColor("TileStartTextPrimaryBrush"));
    }

    [Fact]
    public void LongApplicationNamesCanUseTwoLinesWithoutGrowingTheRow()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var appName = document.Descendants(presentation + "TextBlock")
            .Single(element =>
                (string?)element.Attribute("Text") == "{Binding Name}"
                && (string?)element.Attribute("Grid.Column") == "1"
                && (string?)element.Attribute("Margin") == "8,0,0,0");

        Assert.Equal("Wrap", (string?)appName.Attribute("TextWrapping"));
        Assert.Equal("34", (string?)appName.Attribute("MaxHeight"));
        Assert.Equal("17", (string?)appName.Attribute("LineHeight"));
    }

    [Fact]
    public void ApplicationListViewportDoesNotApplyCompiledBottomPaddingDirectly()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var appsList = document.Descendants(presentation + "ListBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "AppsList");

        Assert.Equal(
            "{x:Static local:Win10VisualMetrics.AllAppsViewportMargin}",
            (string?)appsList.Attribute("Margin"));
        Assert.Null(appsList.Attribute("Padding"));
        Assert.Equal(0, Win10VisualMetrics.AllAppsViewportMargin.Bottom);
    }

    [Fact]
    public void TrayMenuColorTableUsesDarkSurfaceAndAccentSelection()
    {
        var highlight = Color.FromArgb(12, 34, 56);
        var palette = TileStartTrayRenderer.GetPalette(AppThemeStyle.Windows11);
        var colors = new TileStartTrayColorTable(highlight, palette);

        Assert.Equal(palette.Background, colors.ToolStripDropDownBackground);
        Assert.Equal(palette.Border, colors.MenuBorder);
        Assert.Equal(palette.Separator, colors.SeparatorDark);
        Assert.Equal(highlight, colors.MenuItemSelected);
    }

    [Fact]
    public void TileButtonsUseDedicatedSquareGeometry()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStyle");
        var border = style.Descendants(XName.Get("Win10InteractionBorder", "clr-namespace:TileStart.Host"))
            .Single(element => (string?)element.Attribute(x + "Name") == "TileBorder");

        Assert.Equal("{StaticResource TileStartTileCornerRadius}", (string?)border.Attribute("CornerRadius"));
        Assert.Equal("0", ReadThemeResourceValue("Win11Theme.xaml", "CornerRadius", "TileStartTileCornerRadius"));
        Assert.Equal("0", ReadThemeResourceValue("Win10Theme.xaml", "CornerRadius", "TileStartTileCornerRadius"));
    }

    [Fact]
    public void SharedExpanderTemplatePropagatesPrimaryForegroundIntoHeaderAndChevron()
    {
        var document = LoadXaml("SharedStyles.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStartDarkExpanderStyle");
        Assert.Contains(style.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Foreground"
            && (string?)setter.Attribute("Value") == "{DynamicResource TileStartTextPrimaryBrush}");

        var toggle = style.Descendants(presentation + "ToggleButton").Single();
        Assert.Equal("{TemplateBinding Foreground}", (string?)toggle.Attribute("Foreground"));
        var header = style.Descendants(presentation + "ContentPresenter")
            .Single(element => element.Attribute("Content") is not null);
        Assert.Equal("{TemplateBinding Foreground}", (string?)header.Attribute("TextElement.Foreground"));
        var chevron = style.Descendants(presentation + "TextBlock")
            .Single(element => (string?)element.Attribute(x + "Name") == "Chevron");
        Assert.Equal("{TemplateBinding Foreground}", (string?)chevron.Attribute("Foreground"));
    }

    [Theory]
    [InlineData("TileSettingsWindow.xaml", "SettingsExpanderStyle")]
    [InlineData("BackupRestoreWindow.xaml", "BackupExpanderStyle")]
    public void DarkWindowsUseTheSharedExpanderTemplate(string fileName, string styleKey)
    {
        var document = LoadXaml(fileName);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == styleKey);

        Assert.Equal("{StaticResource TileStartDarkExpanderStyle}", (string?)style.Attribute("BasedOn"));
    }

    [Fact]
    public void TileStartSettingsContainsConsolidatedTrayActions()
    {
        var document = LoadXaml("SettingsWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.NotNull(document.Descendants(presentation + "CheckBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "StartupBox"));
        Assert.NotNull(document.Descendants(presentation + "RadioButton")
            .Single(element => (string?)element.Attribute(x + "Name") == "Windows10Choice"));
        Assert.NotNull(document.Descendants(presentation + "RadioButton")
            .Single(element => (string?)element.Attribute(x + "Name") == "Windows11Choice"));
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "检查更新");
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "备份与恢复");
        Assert.Contains(document.Descendants(presentation + "TextBlock"),
            element => (string?)element.Attribute("Text") == "关于 TileStart");
    }

    [Fact]
    public void TilePaneContextMenusUseTheSharedPopupAndItemStyles()
    {
        var document = LoadMainWindow();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var menus = document.Descendants(presentation + "ContextMenu")
            .Where(menu => menu.Descendants(presentation + "MenuItem").Any(item =>
                (string?)item.Attribute("Header") is "新建自定义磁贴…" or "删除组"))
            .ToArray();

        Assert.NotEmpty(menus);
        Assert.All(menus, menu =>
        {
            Assert.Null(menu.Attribute("Style"));
            Assert.All(menu.Descendants(presentation + "MenuItem"), item =>
                Assert.Null(item.Attribute("Style")));
        });

        var sharedStyles = LoadXaml("SharedStyles.xaml");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var implicitContextMenuStyle = sharedStyles.Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute("TargetType") == "ContextMenu"
                && element.Attribute(x + "Key") is null);
        var implicitMenuItemStyle = sharedStyles.Descendants(presentation + "Style")
            .Single(element =>
                (string?)element.Attribute("TargetType") == "MenuItem"
                && element.Attribute(x + "Key") is null);
        Assert.Equal("{StaticResource TileStartContextMenuStyle}",
            (string?)implicitContextMenuStyle.Attribute("BasedOn"));
        Assert.Equal("{StaticResource TileStartMenuItemStyle}",
            (string?)implicitMenuItemStyle.Attribute("BasedOn"));
    }

    [Fact]
    public void Windows11ContextMenusUseAccentColorHighlightWithInsetPadding()
    {
        Assert.Equal("{x:Static local:Win10Theme.AccentColor}",
            ReadThemeBrushColor("TileStartContextMenuHighlightBrush"));
        Assert.Equal("4", ReadThemeResourceValue(
            "Win11Theme.xaml",
            "Thickness",
            "TileStartContextMenuPresenterPadding"));

        var document = LoadXaml("SharedStyles.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var contextMenuStyle = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStartContextMenuStyle");
        Assert.Contains(contextMenuStyle.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Padding"
            && (string?)setter.Attribute("Value") == "{DynamicResource TileStartContextMenuPresenterPadding}");
    }

    private static XDocument LoadMainWindow()
    {
        return LoadXaml("MainWindow.xaml");
    }

    private static XDocument LoadXaml(string fileName) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName));

    private static string? ReadThemeBrushColor(string key)
    {
        var document = LoadXaml("Win11Theme.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document.Descendants(presentation + "SolidColorBrush")
            .Single(element => (string?)element.Attribute(x + "Key") == key)
            .Attribute("Color")?.Value;
    }

    private static string? ReadThemeResourceValue(string fileName, string elementName, string key)
    {
        var document = LoadXaml(fileName);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return document.Descendants(presentation + elementName)
            .Single(element => (string?)element.Attribute(x + "Key") == key)
            .Value;
    }
}