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

    private static XDocument LoadMainWindow()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", "MainWindow.xaml");
        return XDocument.Load(path);
    }
}