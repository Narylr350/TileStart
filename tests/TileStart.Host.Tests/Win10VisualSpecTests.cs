using System.IO;
using System.Text.Json;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10VisualSpecTests
{
    [Fact]
    public void FrameMetricsMatchExtractedStartUiSpec()
    {
        using var spec = ReadSpec("frame.json");
        var metrics = spec.RootElement.GetProperty("metrics");

        Assert.Equal(Win10VisualMetrics.CollapsedNavigationWidth, Value(metrics, "collapsedNavigationWidth"));
        Assert.Equal(Win10VisualMetrics.NavigationItemHeight, Value(metrics, "navigationItemHeight"));
        Assert.Equal(Win10VisualMetrics.AllAppsWidth, Value(metrics, "allAppsWidth"));
        AssertThickness(Win10VisualMetrics.AllAppsMargin, metrics.GetProperty("allAppsMargin").GetProperty("value"));
    }

    [Fact]
    public void AllAppsAndAlphabetMetricsMatchExtractedStartUiSpecs()
    {
        using var allApps = ReadSpec("all-apps.json");
        var allAppsMetrics = allApps.RootElement.GetProperty("metrics");
        Assert.Equal(Win10VisualMetrics.AllAppsRowHeight, Value(allAppsMetrics, "rowHeight"));
        Assert.Equal(Win10VisualMetrics.AllAppsGroupHeaderHeight, Value(allAppsMetrics, "groupHeaderHeight"));
        AssertThickness(Win10VisualMetrics.AllAppsListPadding, allAppsMetrics.GetProperty("listPadding").GetProperty("value"));

        using var alphabet = ReadSpec("alphabet-index.json");
        var alphabetMetrics = alphabet.RootElement.GetProperty("metrics");
        Assert.Equal(Win10VisualMetrics.AlphabetCellSize, Value(alphabetMetrics, "cellSize"));
        Assert.Equal(Win10VisualMetrics.AlphabetFontSize, Value(alphabetMetrics, "fontSize"));
    }

    [Fact]
    public void TileLayoutMetricsMatchExtractedStartUiSpec()
    {
        using var spec = ReadSpec("tile-content.json");
        var metrics = spec.RootElement.GetProperty("metrics");

        Assert.Equal(Win10VisualMetrics.TileGroupHeaderHeight, Value(metrics, "groupHeaderHeight"));
        AssertThickness(Win10VisualMetrics.TileNestedPanelMargin, metrics.GetProperty("nestedPanelMargin").GetProperty("value"));
        Assert.Equal("NoWrap", metrics.GetProperty("groupTitleWrapping").GetProperty("value").GetString());
    }

    [Fact]
    public void UnresolvedIconRulesCannotBeMistakenForVerifiedMetrics()
    {
        using var spec = ReadSpec("icon-resolution.json");

        Assert.Equal("partial-unresolved", spec.RootElement.GetProperty("status").GetString());
        Assert.NotEmpty(spec.RootElement.GetProperty("unresolved").EnumerateArray());
    }

    private static JsonDocument ReadSpec(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "win10-start-specs", fileName)));

    private static double Value(JsonElement metrics, string name) =>
        metrics.GetProperty(name).GetProperty("value").GetDouble();

    private static void AssertThickness(System.Windows.Thickness expected, JsonElement value)
    {
        var actual = value.EnumerateArray().Select(item => item.GetDouble()).ToArray();
        Assert.Equal([expected.Left, expected.Top, expected.Right, expected.Bottom], actual);
    }
}


