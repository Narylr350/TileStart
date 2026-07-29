using TileStart.Host.Tiles.Layout;

namespace TileStart.Host.Windowing;

public static class StartWindowSizing
{
    // Win10 permits the tile pane to be completely collapsed, leaving only
    // the navigation rail and the all-apps list visible.
    public const int MinimumGroupColumns = 0;
    public const int MaximumGroupColumns = 3;
    public const int DefaultWorkspaceColumns = MinimumGroupColumns;

    public static int MinimumColumnsForTileCount(int tileCount) => tileCount > 0 ? 1 : 0;

    public static int MinimumColumnsForTileLayout(int tileCount, int widestGroupWidthUnits)
    {
        if (tileCount <= 0)
        {
            return MinimumGroupColumns;
        }

        var workspaceColumns = (Math.Max(1, widestGroupWidthUnits)
                                + TileWorkspaceMetrics.LegacyGroupWidthUnits - 1)
                               / TileWorkspaceMetrics.LegacyGroupWidthUnits;
        return Math.Clamp(workspaceColumns, 1, MaximumGroupColumns);
    }

    private static double FixedPaneWidth =>
        Win10VisualMetrics.CollapsedNavigationWidth
        + Win10VisualMetrics.AllAppsWidth
        + Win10VisualMetrics.TileScrollViewerLeftMargin;

    public static double WidthForColumns(int columns)
    {
        columns = Math.Clamp(columns, MinimumGroupColumns, MaximumGroupColumns);
        if (columns == 0)
        {
            return FixedPaneWidth;
        }

        return FixedPaneWidth
               + Win10GroupWrapPanel.RequiredWidth(
                   columns * TileWorkspaceMetrics.LegacyGroupWidthUnits)
               + Win10VisualMetrics.TileScrollBarLayoutWidth;
    }

    public static double SnapWidth(double requestedWidth, double availableWidth)
    {
        if (!double.IsFinite(requestedWidth))
        {
            requestedWidth = WidthForColumns(MinimumGroupColumns);
        }

        var candidates = AvailableColumns(availableWidth);
        if (candidates.Length == 0)
        {
            return Math.Max(1, availableWidth);
        }

        return WidthForColumns(NearestColumns(requestedWidth, candidates));
    }

    public static int ColumnsForWidth(double requestedWidth, double availableWidth = double.PositiveInfinity)
    {
        if (!double.IsFinite(requestedWidth))
        {
            return MinimumGroupColumns;
        }

        var candidates = AvailableColumns(availableWidth);
        if (candidates.Length == 0)
        {
            return MinimumGroupColumns;
        }

        return NearestColumns(requestedWidth, candidates);
    }

    private static int[] AvailableColumns(double availableWidth) =>
        Enumerable.Range(
                MinimumGroupColumns,
                MaximumGroupColumns - MinimumGroupColumns + 1)
            .Where(columns => !double.IsFinite(availableWidth) || WidthForColumns(columns) <= availableWidth + 0.1)
            .ToArray();

    private static int NearestColumns(double requestedWidth, IEnumerable<int> candidates) =>
        candidates
            .OrderBy(columns => Math.Abs(WidthForColumns(columns) - requestedWidth))
            .ThenBy(columns => columns)
            .First();

    public static double MaximumWidth(double availableWidth)
    {
        return SnapWidth(WidthForColumns(MaximumGroupColumns), availableWidth);
    }

    public static double ClampHeight(double requestedHeight, double minimumHeight, double availableHeight)
    {
        if (!double.IsFinite(availableHeight) || availableHeight <= 0)
        {
            return Math.Max(minimumHeight, requestedHeight);
        }

        var effectiveMinimum = Math.Min(minimumHeight, availableHeight);
        return Math.Clamp(requestedHeight, effectiveMinimum, availableHeight);
    }
}