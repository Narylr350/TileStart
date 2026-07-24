using System.Drawing;
using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class DarkThemeVisualTests
{
    [Fact]
    public void MainWindowProvidesAWhiteDefaultForTextBlocks()
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
                && (string?)setter.Attribute("Value") == "White");
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
        var colors = new TileStartTrayColorTable(highlight);

        Assert.Equal(TileStartTrayRenderer.BackgroundColor, colors.ToolStripDropDownBackground);
        Assert.Equal(TileStartTrayRenderer.BorderColor, colors.MenuBorder);
        Assert.Equal(TileStartTrayRenderer.SeparatorColor, colors.SeparatorDark);
        Assert.Equal(highlight, colors.MenuItemSelected);
    }

    [Fact]
    public void SharedExpanderTemplatePropagatesWhiteForegroundIntoHeaderAndChevron()
    {
        var document = LoadXaml("App.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = document.Descendants(presentation + "Style")
            .Single(element => (string?)element.Attribute(x + "Key") == "TileStartDarkExpanderStyle");
        Assert.Contains(style.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Foreground"
            && (string?)setter.Attribute("Value") == "White");

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

    private static XDocument LoadMainWindow()
    {
        return LoadXaml("MainWindow.xaml");
    }

    private static XDocument LoadXaml(string fileName) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName));
}