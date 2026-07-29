using TileStart.Host.Tiles.Layout;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tiles.Settings;

public static class TileContextActions
{
    public static bool IsSelectedSize(TileSize currentSize, string? sizeName) =>
        Enum.TryParse<TileSize>(sizeName, out var size) && size == currentSize;

    public static bool Unpin(TileLayout layout, TileItem tile)
    {
        if (!TryFindLocation(layout, tile, out var group, out var folder))
        {
            return false;
        }

        if (folder is not null)
        {
            folder.FolderTiles.Remove(tile);
            TileFolderLayout.Normalize(folder);
            group.RefreshLayout();
            return true;
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
                group.ContentColumns - child.Size.ColumnSpan());
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
        // 文件夹正面的内容预览按 Win10 中磁贴槽位重建，其他尺寸没有对应的
        // 预览布局。入口和领域操作都拒绝调整，避免绕过 UI 写入半支持状态。
        if (tile.IsTileFolder
            || !TryFindLocation(layout, tile, out var group, out var folder)
            || tile.Size == size)
        {
            return false;
        }

        tile.Size = size;
        if (folder is null)
        {
            Win10GroupLayout.Normalize(group);
        }
        else
        {
            TileFolderLayout.Normalize(folder);
            group.RefreshLayout();
        }

        return true;
    }

    private static bool TryFindLocation(
        TileLayout layout,
        TileItem tile,
        out TileGroup group,
        out TileItem? folder)
    {
        foreach (var candidate in layout.Groups)
        {
            if (candidate.Tiles.Contains(tile))
            {
                group = candidate;
                folder = null;
                return true;
            }

            var parentFolder = candidate.Tiles.FirstOrDefault(item =>
                item.IsTileFolder && item.FolderTiles.Contains(tile));
            if (parentFolder is not null)
            {
                group = candidate;
                folder = parentFolder;
                return true;
            }
        }

        group = null!;
        folder = null;
        return false;
    }
}