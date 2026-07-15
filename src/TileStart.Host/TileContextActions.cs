namespace TileStart.Host;

public static class TileContextActions
{
    public static bool Unpin(TileLayout layout, TileItem tile)
    {
        var group = layout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(tile));
        if (group is null)
        {
            return false;
        }

        group.Tiles.Remove(tile);
        if (group.Tiles.Count == 0)
        {
            TileGroupManager.Remove(layout, group);
        }
        else
        {
            Win10GroupLayout.Normalize(group);
        }

        return true;
    }

    public static bool Resize(TileLayout layout, TileItem tile, TileSize size)
    {
        var group = layout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(tile));
        if (group is null || tile.Size == size)
        {
            return false;
        }

        tile.Size = size;
        Win10GroupLayout.Normalize(group);
        return true;
    }
}
