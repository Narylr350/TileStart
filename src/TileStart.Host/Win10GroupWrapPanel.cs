namespace TileStart.Host;

public sealed class Win10GroupWrapPanel : System.Windows.Controls.Panel
{
    private const double LayoutRoundingAllowance = 1;
    internal static int ColumnsForWidth(double availableWidth)
    {
        if (!double.IsFinite(availableWidth))
        {
            return int.MaxValue;
        }

        return Math.Max(1, (int)Math.Floor(
            (availableWidth
             + Win10TileMetrics.GroupGap
             + LayoutRoundingAllowance)
            / Win10TileMetrics.GroupPitch));
    }

    internal static double RequiredWidth(int columns)
    {
        return columns <= 0
            ? 0
            : columns * Win10TileMetrics.GroupWidth + (columns - 1) * Win10TileMetrics.GroupGap;
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        var columns = Math.Min(InternalChildren.Count, ColumnsForWidth(availableSize.Width));
        if (columns <= 0)
        {
            return new System.Windows.Size();
        }

        foreach (System.Windows.UIElement child in InternalChildren)
        {
            child.Measure(new System.Windows.Size(Win10TileMetrics.GroupWidth, double.PositiveInfinity));
        }

        var rowHeights = GetRowHeights(columns);
        return new System.Windows.Size(
            Math.Min(availableSize.Width, RequiredWidth(columns)),
            rowHeights.Sum());
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        var columns = Math.Min(InternalChildren.Count, ColumnsForWidth(finalSize.Width));
        if (columns <= 0)
        {
            return finalSize;
        }

        var rowHeights = GetRowHeights(columns);
        var y = 0.0;
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            if (column == 0 && row > 0)
            {
                y += rowHeights[row - 1];
            }

            InternalChildren[index].Arrange(new System.Windows.Rect(
                column * Win10TileMetrics.GroupPitch,
                y,
                Win10TileMetrics.GroupWidth,
                rowHeights[row]));
        }

        return finalSize;
    }

    private double[] GetRowHeights(int columns)
    {
        var rowHeights = new double[(InternalChildren.Count + columns - 1) / columns];
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var row = index / columns;
            rowHeights[row] = Math.Max(rowHeights[row], InternalChildren[index].DesiredSize.Height);
        }

        return rowHeights;
    }
}
