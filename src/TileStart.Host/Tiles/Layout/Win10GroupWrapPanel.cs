using System.Windows;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tiles.Layout;

internal readonly record struct Win10GroupPanelItem(
    int Index,
    int Column,
    int Row,
    int ColumnSpan,
    double Width,
    double Height)
{
    public Win10GroupPanelItem(int index, int column, int row, double height)
        : this(
            index,
            column,
            row,
            TileWorkspaceMetrics.LegacyGroupWidthUnits,
            TileWorkspaceMetrics.GroupVisualWidth(TileWorkspaceMetrics.LegacyGroupWidthUnits),
            height)
    {
    }
}

internal readonly record struct Win10GroupPanelSlot(
    int Index,
    int Column,
    int Row,
    int ColumnSpan,
    double Left,
    double Top,
    double Width,
    double Height);

public sealed class Win10GroupWrapPanel : System.Windows.Controls.Panel
{
    internal static int ColumnsForWidth(double availableWidth) =>
        TileWorkspaceMetrics.ColumnsForWidth(availableWidth);

    internal static double RequiredWidth(int columns) =>
        TileWorkspaceMetrics.RequiredWidth(columns);

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
        var occupied = new Dictionary<int, HashSet<int>>();
        var resolved = new List<(Win10GroupPanelItem Item, TileGroupCell Cell)>(items.Count);
        foreach (var item in items)
        {
            var span = Math.Clamp(item.ColumnSpan, 1, columns);
            var requested = new TileGroupCell(item.Column, item.Row);
            var cell = IsAvailable(requested, span, columns, occupied)
                ? requested
                : FindFirstAvailable(span, columns, occupied);
            Occupy(cell, span, occupied);
            resolved.Add((item with { ColumnSpan = span }, cell));
        }

        var rowHeights = resolved
            .GroupBy(entry => entry.Cell.Row)
            .ToDictionary(row => row.Key, row => row.Max(entry => entry.Item.Height));
        var rowTops = new Dictionary<int, double>();
        var top = 0d;
        for (var row = 0; row <= rowHeights.Keys.DefaultIfEmpty(-1).Max(); row++)
        {
            rowTops[row] = top;
            if (rowHeights.TryGetValue(row, out var height))
            {
                top += height;
            }
        }

        return
        [
            .. resolved
                .Select(entry => new Win10GroupPanelSlot(
                    entry.Item.Index,
                    entry.Cell.Column,
                    entry.Cell.Row,
                    entry.Item.ColumnSpan,
                    TileWorkspaceMetrics.Left(entry.Cell.Column),
                    rowTops[entry.Cell.Row],
                    entry.Item.Width,
                    entry.Item.Height))
                .OrderBy(slot => slot.Index),
        ];
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        if (InternalChildren.Count == 0)
        {
            return new System.Windows.Size();
        }

        foreach (System.Windows.UIElement child in InternalChildren)
        {
            var group = (child as FrameworkElement)?.DataContext as TileGroup;
            var childWidth = group?.VisualWidth ?? Win10VisualMetrics.TileGroupVisualWidth;
            child.Measure(new System.Windows.Size(childWidth, double.PositiveInfinity));
        }

        var columns = GetColumnCount(availableSize.Width);
        var slots = CalculateSlots(CreateItems(), columns);
        var usedColumns = slots.Max(slot => slot.Column + slot.ColumnSpan);
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
                slot.Left,
                slot.Top,
                slot.Width,
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
                    group?.WidthUnits ?? TileWorkspaceMetrics.LegacyGroupWidthUnits,
                    group?.VisualWidth ?? child.DesiredSize.Width,
                    child.DesiredSize.Height);
            })
            .ToArray();
    }

    private int GetColumnCount(double availableWidth)
    {
        var minimum = InternalChildren
            .Cast<System.Windows.UIElement>()
            .Select(child => (child as FrameworkElement)?.DataContext as TileGroup)
            .Where(group => group is not null)
            .Select(group => group!.WidthUnits)
            .DefaultIfEmpty(1)
            .Max();
        if (double.IsFinite(availableWidth))
        {
            return Math.Max(minimum, ColumnsForWidth(availableWidth));
        }

        var configuredColumns = InternalChildren
            .Cast<System.Windows.UIElement>()
            .Select(child => (child as FrameworkElement)?.DataContext as TileGroup)
            .Where(group => group is not null)
            .Select(group => group!.GroupColumn + group.WidthUnits)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(minimum, configuredColumns);
    }

    private static bool IsAvailable(
        TileGroupCell cell,
        int span,
        int columns,
        IReadOnlyDictionary<int, HashSet<int>> occupied)
    {
        if (cell.Column < 0 || cell.Row < 0 || cell.Column + span > columns)
        {
            return false;
        }

        return !occupied.TryGetValue(cell.Row, out var row)
               || Enumerable.Range(cell.Column, span).All(column => !row.Contains(column));
    }

    private static TileGroupCell FindFirstAvailable(
        int span,
        int columns,
        IReadOnlyDictionary<int, HashSet<int>> occupied)
    {
        for (var row = 0;; row++)
        {
            for (var column = 0; column <= columns - span; column++)
            {
                var cell = new TileGroupCell(column, row);
                if (IsAvailable(cell, span, columns, occupied))
                {
                    return cell;
                }
            }
        }
    }

    private static void Occupy(
        TileGroupCell cell,
        int span,
        IDictionary<int, HashSet<int>> occupied)
    {
        if (!occupied.TryGetValue(cell.Row, out var row))
        {
            row = [];
            occupied.Add(cell.Row, row);
        }

        for (var column = cell.Column; column < cell.Column + span; column++)
        {
            row.Add(column);
        }
    }
}