using System.IO;
using System.Text.Json;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class Win10VisualSpecTests
{
    [Fact]
    public void GridDefinitionsExposeGridLengthValuesForXaml()
    {
        Assert.Equal(Win10VisualMetrics.CollapsedNavigationWidth, Win10VisualMetrics.CollapsedNavigationGridLength.Value);
        Assert.Equal(Win10VisualMetrics.NavigationItemHeight, Win10VisualMetrics.NavigationItemGridLength.Value);
        Assert.Equal(Win10VisualMetrics.AllAppsWidth, Win10VisualMetrics.AllAppsGridLength.Value);
    }

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
        Assert.Equal(Win10VisualMetrics.AllAppsGridItemWidth, Value(allAppsMetrics, "gridItemWidth"));
        Assert.Equal(Win10VisualMetrics.AllAppsRowHeight, Value(allAppsMetrics, "rowHeight"));
        Assert.Equal(Win10VisualMetrics.AllAppsGroupHeaderHeight, Value(allAppsMetrics, "groupHeaderHeight"));
        AssertThickness(Win10VisualMetrics.AllAppsListPadding, allAppsMetrics.GetProperty("listPadding").GetProperty("value"));
        Assert.Equal(
            (Win10VisualMetrics.AllAppsWidth - Win10VisualMetrics.AllAppsGridItemWidth) / 2,
            Win10VisualMetrics.AllAppsItemMargin.Left);
        Assert.Equal(Win10VisualMetrics.AllAppsItemMargin.Left, Win10VisualMetrics.AllAppsItemMargin.Right);

        using var alphabet = ReadSpec("alphabet-index.json");
        var alphabetMetrics = alphabet.RootElement.GetProperty("metrics");
        Assert.Equal(Win10VisualMetrics.AlphabetCellSize, Value(alphabetMetrics, "cellSize"));
        Assert.Equal(Win10VisualMetrics.AlphabetFontSize, Value(alphabetMetrics, "fontSize"));
    }

    [Fact]
    public void ContextMenuMetricsMatchExtractedStartUiSpec()
    {
        using var spec = ReadSpec("context-menu.json");
        var metrics = spec.RootElement.GetProperty("metrics");

        Assert.Equal(Win10VisualMetrics.ContextMenuFontSize, Value(metrics, "fontSize"));
        Assert.Equal(Win10VisualMetrics.ContextMenuMinWidth, Value(metrics, "minimumWidth"));
        Assert.Equal(Win10VisualMetrics.ContextMenuCheckPlaceholderWidth, Value(metrics, "checkPlaceholderWidth"));
        Assert.Equal(Win10VisualMetrics.ContextMenuIconPlaceholderWidth, Value(metrics, "iconPlaceholderWidth"));
        AssertThickness(Win10VisualMetrics.ContextMenuItemPadding, metrics.GetProperty("itemPaddingMouse").GetProperty("value"));
        AssertThickness(Win10VisualMetrics.ContextMenuPresenterPadding, metrics.GetProperty("presenterPadding").GetProperty("value"));
        Assert.NotEmpty(spec.RootElement.GetProperty("unresolved").EnumerateArray());
    }

    [Fact]
    public void TileLayoutMetricsMatchExtractedStartUiSpec()
    {
        using var spec = ReadSpec("tile-content.json");
        var metrics = spec.RootElement.GetProperty("metrics");

        Assert.Equal(Win10VisualMetrics.TileGroupHeaderHeight, Value(metrics, "groupHeaderHeight"));
        Assert.Equal(Win10VisualMetrics.TileReservedBrandingSpace, Value(metrics, "reservedBrandingSpace"));
        Assert.Equal(
            Win10VisualMetrics.TileGroupVisualWidth,
            Win10TileMetrics.GroupWidth + Win10VisualMetrics.TileNestedPanelMargin.Left +
            Win10VisualMetrics.TileNestedPanelMargin.Right);
        Assert.Equal(
            Win10VisualMetrics.TileGroupVisualGap,
            Win10TileMetrics.GroupPitch - Win10VisualMetrics.TileGroupVisualWidth);
        Assert.Equal(Win10VisualMetrics.TileReservedBrandingSpace, Win10VisualMetrics.TileReservedBrandingGridLength.Value);
        AssertThickness(Win10VisualMetrics.TileNestedPanelMargin, metrics.GetProperty("nestedPanelMargin").GetProperty("value"));
        AssertThickness(Win10VisualMetrics.TileBrandingMargin, metrics.GetProperty("bottomAlignedTextMargin").GetProperty("value"), horizontalInset: 8);
        Assert.Equal("NoWrap", metrics.GetProperty("groupTitleWrapping").GetProperty("value").GetString());
    }

    [Fact]
    public void TileGroupMetricsMatchExtractedStartUiSpec()
    {
        using var spec = ReadSpec("tile-group.json");
        var metrics = spec.RootElement.GetProperty("metrics");

        Assert.Equal(Win10VisualMetrics.TileGroupHeaderHeight, Value(metrics, "headerHeight"));
        Assert.Equal(Win10VisualMetrics.TileGroupHeaderFontSize, Value(metrics, "titleFontSize"));
        Assert.Equal(Win10VisualMetrics.TileGroupGripperWidth, Value(metrics, "gripperColumnWidth"));
        Assert.Equal(Win10VisualMetrics.TileGroupGripperFontSize, Value(metrics, "gripperFontSize"));
        Assert.Equal(Win10VisualMetrics.TileGroupHeaderStrokeThickness, Value(metrics, "backgroundStrokeThickness"));
        Assert.Equal(Win10VisualMetrics.TileGroupPrimaryFocusThickness, Value(metrics, "primaryFocusThickness"));
        Assert.Equal(Win10VisualMetrics.TileGroupSecondaryFocusThickness, Value(metrics, "secondaryFocusThickness"));
        Assert.Equal(Win10VisualMetrics.TileGroupGripperWidth, Win10VisualMetrics.TileGroupGripperGridLength.Value);
        AssertThickness(Win10VisualMetrics.TileGroupTitleRestMargin, metrics.GetProperty("titleMarginRest").GetProperty("value"));
        AssertThickness(Win10VisualMetrics.TileGroupTitleInteractiveMargin, metrics.GetProperty("titleMarginInteractive").GetProperty("value"));
        AssertThickness(Win10VisualMetrics.TileGroupGripperMargin, metrics.GetProperty("gripperMargin").GetProperty("value"));
        Assert.NotEmpty(spec.RootElement.GetProperty("unresolved").EnumerateArray());
    }

    [Fact]
    public void AppListIconMetricsMatchSymbolDerivedSpec()
    {
        using var spec = ReadSpec("icon-resolution.json");
        var mappings = spec.RootElement.GetProperty("derivedMappings");
        var themeAware = mappings.GetProperty("appListLogoSizeDip").GetProperty("themeAware").EnumerateArray().ToArray();
        var legacy = mappings.GetProperty("appListLogoSizeDip").GetProperty("legacy").EnumerateArray().ToArray();

        foreach (var item in themeAware)
        {
            var type = item.GetProperty("appItemLogoType").GetInt32();
            Assert.Equal(Win10IconMetrics.GetAppListImageSize(type, themeAware: true), item.GetProperty("image").GetDouble());
            Assert.Equal(Win10IconMetrics.GetAppListLayoutSize(type), item.GetProperty("layout").GetDouble());
        }

        foreach (var item in legacy)
        {
            var type = item.GetProperty("appItemLogoType").GetInt32();
            Assert.Equal(Win10IconMetrics.GetAppListImageSize(type, themeAware: false), item.GetProperty("image").GetDouble());
            Assert.Equal(Win10IconMetrics.GetAppListLayoutSize(type), item.GetProperty("layout").GetDouble());
        }

        Assert.Equal(Win10IconMetrics.ClassicAppLogoImageSize, legacy[0].GetProperty("image").GetDouble());
        Assert.Equal(Win10IconMetrics.ClassicAppLogoLayoutSize, legacy[0].GetProperty("layout").GetDouble());
    }

    [Fact]
    public void UnresolvedIconRulesCannotBeMistakenForVerifiedMetrics()
    {
        using var spec = ReadSpec("icon-resolution.json");

        Assert.StartsWith("partial-", spec.RootElement.GetProperty("status").GetString());
        Assert.NotEmpty(spec.RootElement.GetProperty("unresolved").EnumerateArray());
        if (spec.RootElement.GetProperty("schemaVersion").GetInt32() >= 2)
        {
            Assert.NotEmpty(spec.RootElement.GetProperty("functions").EnumerateArray());
            Assert.Empty(spec.RootElement.GetProperty("missingRequestedSymbols").EnumerateArray());
        }
    }

    private static JsonDocument ReadSpec(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "win10-start-specs", fileName)));

    private static double Value(JsonElement metrics, string name) =>
        metrics.GetProperty(name).GetProperty("value").GetDouble();

    private static void AssertThickness(System.Windows.Thickness expected, JsonElement value, double horizontalInset = 0)
    {
        var actual = value.EnumerateArray().Select(item => item.GetDouble()).ToArray();
        Assert.Equal([expected.Left - horizontalInset, expected.Top, expected.Right - horizontalInset, expected.Bottom], actual);
    }
}

