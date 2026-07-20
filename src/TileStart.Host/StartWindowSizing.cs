namespace TileStart.Host;

public static class StartWindowSizing
{
    public const int MinimumGroupColumns = 1;
    public const int MaximumGroupColumns = 3;

    private static double FixedPaneWidth =>
        Win10VisualMetrics.CollapsedNavigationWidth
        + Win10VisualMetrics.AllAppsWidth
        + Win10VisualMetrics.TileScrollViewerLeftMargin;

    public static double WidthForColumns(int columns)
    {
        columns = Math.Clamp(columns, MinimumGroupColumns, MaximumGroupColumns);
        return FixedPaneWidth
               + Win10GroupWrapPanel.RequiredWidth(columns)
               + Win10VisualMetrics.TileScrollBarLayoutWidth;
    }

    public static double SnapWidth(double requestedWidth, double availableWidth)
    {
        if (!double.IsFinite(requestedWidth))
        {
            requestedWidth = WidthForColumns(MinimumGroupColumns);
        }

        var candidates = Enumerable.Range(MinimumGroupColumns, MaximumGroupColumns)
            .Select(WidthForColumns)
            .Where(width => !double.IsFinite(availableWidth) || width <= availableWidth + 0.1)
            .ToArray();
        if (candidates.Length == 0)
        {
            return Math.Max(1, availableWidth);
        }

        return candidates
            .OrderBy(width => Math.Abs(width - requestedWidth))
            .ThenBy(width => width)
            .First();
    }

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
