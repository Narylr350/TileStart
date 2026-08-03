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

    [Fact]
    public void StartWindowPositionsBeforeApplyingAcrylicMaterial()
    {
        var showMethod = ReadShowFromShellMethod();
        var materialIndex = showMethod.IndexOf("ApplyWindowMaterial();", StringComparison.Ordinal);
        var positionIndex = showMethod.IndexOf("PositionOnCurrentMonitor();", StringComparison.Ordinal);

        Assert.True(positionIndex >= 0);
        Assert.True(materialIndex > positionIndex);
    }

    [Fact]
    public void StartWindowRebuildsTheNativeFrameAfterPositioning()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Performance", "StartWindowController.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("SwpNoActivate | SwpFrameChanged", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayChangesUseDedicatedDpiAndWorkAreaPaths()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Performance", "StartWindowController.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("HandleDpiChanged(window, lParam);", source, StringComparison.Ordinal);
        Assert.Contains("message == WmSettingChange && wParam.ToInt64() == SpiSetWorkArea", source,
            StringComparison.Ordinal);
        Assert.Contains("MonitorFromRect(ref suggested, MonitorDefaultToNearest)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PositionOnCurrentMonitor(WindowSizeStore.Load())", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticWorkspaceMinimumDoesNotOverwriteTheUserPreference()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Performance", "StartWindowController.cs");
        var source = File.ReadAllText(path);
        var method = source[source.IndexOf("public void SetMinimumWorkspaceColumns", StringComparison.Ordinal)..];
        method = method[..method.IndexOf("public void SetWindowSource", StringComparison.Ordinal)];

        Assert.DoesNotContain("_preferredWorkspaceColumns =", method, StringComparison.Ordinal);
        Assert.DoesNotContain("PersistPreferredSize", method, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowWidthSnapDoesNotRelayoutTheWorkspaceEveryRenderFrame()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Performance", "StartWindowController.cs");
        var source = File.ReadAllText(path);
        var method = source[source.IndexOf("private void SnapWindowWidthAfterResize()", StringComparison.Ordinal)..];
        method = method[..method.IndexOf("private void BeginLiveResize()", StringComparison.Ordinal)];

        Assert.DoesNotContain("CompositionTarget.Rendering += WindowWidthSnap_Rendering", source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("private void WindowWidthSnap_Rendering", source, StringComparison.Ordinal);
        Assert.Contains("_window.Width = targetWidth;", method, StringComparison.Ordinal);
        Assert.Contains("_animateGroupReorderFrom(previousGroupPositions);", method, StringComparison.Ordinal);
        Assert.True(
            method.IndexOf("_preferredWorkspaceColumns = calculatedColumns;", StringComparison.Ordinal)
            < method.IndexOf("PositionOnCurrentMonitor();", StringComparison.Ordinal),
            "The snapped column preference must be published before placement reads it.");
    }

    [Fact]
    public void LiveResizeTemporarilyDisablesAcrylicComposition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Performance", "StartWindowController.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("message == WmEnterSizeMove", source, StringComparison.Ordinal);
        Assert.Contains("message == WmExitSizeMove", source, StringComparison.Ordinal);
        Assert.Contains("SetAccentPolicy(0, 0, 0);", source, StringComparison.Ordinal);
        Assert.Contains("var material = Win10Theme.ReadStartMaterial(_themeStyle);", source, StringComparison.Ordinal);
        Assert.Contains("new SolidColorBrush(material.LiveResizeColor)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new SolidColorBrush(Win10Theme.StartSurfaceColor)", source,
            StringComparison.Ordinal);
        Assert.Contains("ApplyWindowMaterial();", source, StringComparison.Ordinal);
    }

    private static string ReadShowFromShellMethod()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Performance", "StartWindowController.cs");
        var source = File.ReadAllText(path);
        var showMethod = source[source.IndexOf("public void ShowFromShell()", StringComparison.Ordinal)..];
        return showMethod[..showMethod.IndexOf("public void AllowClose()", StringComparison.Ordinal)];
    }
}
