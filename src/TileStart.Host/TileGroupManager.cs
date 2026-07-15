namespace TileStart.Host;

public static class TileGroupManager
{
    public static TileGroup Add(TileLayout layout, string name = "")
    {
        var group = new TileGroup { Name = name };
        layout.Groups.Add(group);
        return group;
    }

    public static bool Remove(TileLayout layout, TileGroup group)
    {
        return layout.Groups.Remove(group);
    }

    public static bool Move(TileLayout layout, TileGroup group, int offset)
    {
        var index = layout.Groups.IndexOf(group);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= layout.Groups.Count)
        {
            return false;
        }

        layout.Groups.Move(index, target);
        return true;
    }
}
