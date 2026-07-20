namespace TileStart.Host;

public static class TileContextActions
{
    public static bool IsSelectedSize(TileSize currentSize, string? sizeName) =>
        Enum.TryParse<TileSize>(sizeName, out var size) && size == currentSize;

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

    public static bool DissolveFolder(TileLayout layout, TileItem folder)
    {
        var group = layout.Groups.FirstOrDefault(candidate => candidate.Tiles.Contains(folder));
        if (group is null || !folder.IsTileFolder)
        {
            return false;
        }

        var originColumn = folder.Column;
        var originRow = folder.Row;
        var children = folder.FolderTiles
            .OrderBy(child => child.Row)
            .ThenBy(child => child.Column)
            .ToArray();

        group.Tiles.Remove(folder);
        folder.IsFolderExpanded = false;
        folder.FolderTiles.Clear();
        foreach (var child in children)
        {
            child.Column = Math.Clamp(
                originColumn + child.Column,
                0,
                Win10TileMetrics.GroupColumns - child.Size.ColumnSpan());
            child.Row = Math.Max(0, originRow + child.Row);
            group.Tiles.Add(child);
        }

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