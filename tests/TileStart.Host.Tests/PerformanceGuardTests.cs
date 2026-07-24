using System.IO;
using System.Xml.Linq;

namespace TileStart.Host.Tests;

public sealed class PerformanceGuardTests
{
    [Fact]
    public void MainApplicationListUsesGroupedContainerRecycling()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", "MainWindow.xaml");
        var document = XDocument.Load(path);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var appsList = document.Descendants(presentation + "ListBox")
            .Single(element => (string?)element.Attribute(x + "Name") == "AppsList");
        var attributes = appsList.Attributes().ToDictionary(attribute => attribute.Name.LocalName);

        Assert.Equal("True", attributes["VirtualizingPanel.IsVirtualizing"].Value);
        Assert.Equal("True", attributes["VirtualizingPanel.IsVirtualizingWhenGrouping"].Value);
        Assert.Equal("Recycling", attributes["VirtualizingPanel.VirtualizationMode"].Value);
        Assert.Equal("Pixel", attributes["VirtualizingPanel.ScrollUnit"].Value);
        Assert.Equal("True", attributes["ScrollViewer.CanContentScroll"].Value);
        Assert.Equal("True", attributes["SmoothScroll.IsEnabled"].Value);
        Assert.NotEmpty(appsList.Descendants(presentation + "VirtualizingStackPanel"));
    }

    [Theory]
    [InlineData("MainWindow.xaml", "AppsList")]
    [InlineData("MainWindow.xaml", "TileScrollViewer")]
    [InlineData("SvgIconWindow.xaml", "SourceBox")]
    public void PrimaryScrollableSurfacesEnableSmoothScrolling(string fileName, string elementName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Xaml", fileName);
        var document = XDocument.Load(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var element = document.Descendants()
            .Single(candidate => (string?)candidate.Attribute(x + "Name") == elementName);

        Assert.Contains(
            element.Attributes(),
            attribute => attribute.Name.LocalName == "SmoothScroll.IsEnabled" && attribute.Value == "True");
    }

    [Theory]
    [InlineData("StartMotion.cs")]
    [InlineData("SemanticZoomMotion.cs")]
    [InlineData("Win10MenuPopupMotion.cs")]
    [InlineData("Win10FolderMotion.cs")]
    public void AnimationsDoNotForceADeviceSpecificFrameRate(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Performance", fileName);
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("SetDesiredFrameRate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DesiredFrameRate = 240", source, StringComparison.Ordinal);
    }
}
