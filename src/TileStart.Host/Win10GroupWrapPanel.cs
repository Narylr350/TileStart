using System.Windows;

namespace TileStart.Host;

internal readonly record struct Win10GroupPanelItem(
    int Index,
    int Column,
    int Row,
    double Height);

internal readonly record struct Win10GroupPanelSlot(
    int Index,
    int Column,
    int Row,
    double Top,
    double Height);

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
             + Win10VisualMetrics.TileGroupVisualGap
             + LayoutRoundingAllowance)
            / Win10TileMetrics.GroupPitch));
    }

    internal static double RequiredWidth(int columns)
    {
        return columns <= 0
            ? 0
            : (columns - 1) * Win10TileMetrics.GroupPitch + Win10VisualMetrics.TileGroupVisualWidth;
    }

    internal static double OverlayClearanceDeficit(
        double viewportWidth,
        int columns,
        double scrollBarFootprint)
    {
        if (!double.IsFinite(viewportWidth) || !double.IsFinite(scrollBarFootprint))
        {
            return 0;
        }

        return Math.Max(0, RequiredWidth(columns) + Math.Max(0, scrollBarFootprint) - viewportWidth);
    }

    internal static Win10GroupPanelSlot[] CalculateSlots(
        IReadOnlyList<Win10GroupPanelItem> items,
        int columns)
    {
        columns = Math.Max(1, columns);
        var occupied = new HashSet<TileGroupCell>();
        var resolved = new List<(Win10GroupPanelItem Item, TileGroupCell Cell)>(items.Count);
        foreach (var item in items)
        {
            var requested = new TileGroupCell(item.Column, item.Row);
            var cell = item.Column >= 0
                       && item.Column < columns
                       && item.Row >= 0
                       && occupied.Add(requested)
                ? requested
                : FindFirstAvailable(occupied, columns);
            occupied.Add(cell);
            resolved.Add((item, cell));
        }

        var slots = new List<Win10GroupPanelSlot>(items.Count);
        foreach (var column in resolved.GroupBy(item => item.Cell.Column))
        {
            var top = 0d;
            foreach (var entry in column.OrderBy(item => item.Cell.Row))
            {
                slots.Add(new Win10GroupPanelSlot(
                    entry.Item.Index,
                    entry.Cell.Column,
                    entry.Cell.Row,
                    top,
                    entry.Item.Height));
                top += entry.Item.Height;
            }
        }

        return [.. slots.OrderBy(slot => slot.Index)];
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        if (InternalChildren.Count == 0)
        {
            return new System.Windows.Size();
        }

        foreach (System.Windows.UIElement child in InternalChildren)
        {
            child.Measure(new System.Windows.Size(Win10VisualMetrics.TileGroupVisualWidth, double.PositiveInfinity));
        }

        var columns = GetColumnCount(availableSize.Width);
        var slots = CalculateSlots(CreateItems(), columns);
        var usedColumns = slots.Max(slot => slot.Column) + 1;
        var height = slots.Max(slot => slot.Top + slot.Height);
        var width = RequiredWidth(usedColumns);
        return new System.Windows.Size(
            double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, width) : width,
            height);
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        if (InternalChildren.Count == 0)
        {
            return finalSize;
        }

        var slots = CalculateSlots(CreateItems(), GetColumnCount(finalSize.Width));
        foreach (var slot in slots)
        {
            InternalChildren[slot.Index].Arrange(new System.Windows.Rect(
                slot.Column * Win10TileMetrics.GroupPitch,
                slot.Top,
                Win10VisualMetrics.TileGroupVisualWidth,
                slot.Height));
        }

        return finalSize;
    }

    private Win10GroupPanelItem[] CreateItems()
    {
        return InternalChildren
            .Cast<System.Windows.UIElement>()
            .Select((child, index) =>
            {
                var group = (child as FrameworkElement)?.DataContext as TileGroup;
                return new Win10GroupPanelItem(
                    index,
                    group?.GroupColumn ?? -1,
                    group?.GroupRow ?? -1,
                    child.DesiredSize.Height);
            })
            .ToArray();
    }

    private int GetColumnCount(double availableWidth)
    {
        if (double.IsFinite(availableWidth))
        {
            return ColumnsForWidth(availableWidth);
        }

        var configuredColumns = InternalChildren
            .Cast<System.Windows.UIElement>()
            .Select(child => (child as FrameworkElement)?.DataContext as TileGroup)
            .Where(group => group is not null)
            .Select(group => group!.GroupColumn + 1)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(1, Math.Max(configuredColumns, InternalChildren.Count));
    }

    private static TileGroupCell FindFirstAvailable(HashSet<TileGroupCell> occupied, int columns)
    {
        for (var row = 0;; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cell = new TileGroupCell(column, row);
                if (!occupied.Contains(cell))
                {
                    return cell;
                }
            }
        }
    }
}
