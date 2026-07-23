namespace TileStart.Host;

public static class TileGroupManager
{
    public static TileGroup Add(TileLayout layout, string name = "")
    {
        return Add(layout, InferColumns(layout), name);
    }

    public static TileGroup Add(TileLayout layout, int columns, string name = "")
    {
        var cell = Win10GroupGridLayout.FindAppendCell(layout, columns);
        var group = new TileGroup { Name = name };
        layout.Groups.Add(group);
        Win10GroupGridLayout.Insert(layout, group, cell, columns);
        return group;
    }

    public static bool Remove(TileLayout layout, TileGroup group)
    {
        return Win10GroupGridLayout.Remove(layout, group);
    }

    public static bool Move(TileLayout layout, TileGroup group, int offset)
    {
        return Move(layout, group, offset, InferColumns(layout));
    }

    public static bool Move(TileLayout layout, TileGroup group, int offset, int columns)
    {
        if (!layout.Groups.Contains(group))
        {
            return false;
        }

        var targetColumn = group.GroupColumn + offset * group.WidthUnits;
        return targetColumn >= 0
               && targetColumn < columns
               && Win10GroupGridLayout.Move(
                   layout,
                   group,
                   new TileGroupCell(targetColumn, group.GroupRow),
                   columns);
    }

    private static int InferColumns(TileLayout layout) =>
        Math.Max(
            TileWorkspaceMetrics.LegacyGroupWidthUnits,
            layout.Groups
                .Select(group => group.GroupColumn + group.WidthUnits)
                .DefaultIfEmpty(TileWorkspaceMetrics.LegacyGroupWidthUnits)
                .Max());
}