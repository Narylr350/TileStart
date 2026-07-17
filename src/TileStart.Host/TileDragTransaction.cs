namespace TileStart.Host;

public sealed class TileDragTransaction : IDisposable
{
    private readonly TileLayout _layout;
    private readonly TileGroup _source;
    private readonly TileItem? _sourceFolder;
    private readonly TileItem _tile;
    private readonly GroupSnapshot[] _snapshots;
    private TileGroup? _previewTarget;
    private int _previewColumn = -1;
    private int _previewRow = -1;
    private string? _previewFolderTargetId;
    private bool _committed;

    public TileDragTransaction(TileLayout layout, TileGroup source, TileItem tile)
        : this(layout, source, null, tile)
    {
    }

    public TileDragTransaction(TileLayout layout, TileGroup source, TileItem? sourceFolder, TileItem tile)
    {
        var sourceContainsTile = sourceFolder is null
            ? source.Tiles.Contains(tile)
            : source.Tiles.Contains(sourceFolder) && sourceFolder.FolderTiles.Contains(tile);
        if (!sourceContainsTile)
        {
            throw new ArgumentException("The source does not contain the tile.", nameof(tile));
        }

        _layout = layout;
        _source = source;
        _sourceFolder = sourceFolder;
        _tile = tile;
        _snapshots = layout.Groups.Select(GroupSnapshot.Capture).ToArray();
    }

    public TileGroup? PreviewTarget => _previewTarget;
    public TileDropIntent Intent { get; private set; }

    public bool Preview(TileGroup target, int column, int row)
    {
        if (Intent == TileDropIntent.Reposition
            && _previewTarget == target
            && (!_snapshots.Any(snapshot => snapshot.Group == target)
                || _previewColumn == column && _previewRow == row))
        {
            return true;
        }

        Restore();
        var moved = _sourceFolder is null
            ? Win10GroupLayout.Move(_source, target, _tile, column, row)
            : MoveFolderChildToGroup(target, column, row);
        if (!moved)
        {
            return false;
        }

        _previewTarget = target;
        _previewColumn = column;
        _previewRow = row;
        _previewFolderTargetId = null;
        Intent = TileDropIntent.Reposition;
        return true;
    }

    public bool PreviewFolder(TileGroup target, TileItem folderTarget)
    {
        if (ReferenceEquals(folderTarget, _tile)
            || _tile.IsTileFolder
            || !target.Tiles.Contains(folderTarget))
        {
            return false;
        }

        if (Intent == TileDropIntent.CreateFolder
            && _previewTarget == target
            && folderTarget.IsTileFolder
            && folderTarget.FolderTiles.Contains(_tile))
        {
            return true;
        }

        if (Intent is TileDropIntent.CreateFolder or TileDropIntent.AddToFolder
            && _previewTarget == target
            && _previewFolderTargetId == folderTarget.Id)
        {
            return true;
        }

        Restore();
        if (!target.Tiles.Contains(folderTarget))
        {
            return false;
        }

        RemoveFromSource();
        if (folderTarget.IsTileFolder)
        {
            var (column, row) = FindFolderAppendPosition(folderTarget, _tile);
            _tile.Column = column;
            _tile.Row = row;
            folderTarget.FolderTiles.Add(_tile);
            TileFolderLayout.Normalize(folderTarget);
            Intent = TileDropIntent.AddToFolder;
        }
        else
        {
            target.Tiles.Remove(folderTarget);
            var folderColumn = folderTarget.Column;
            var folderRow = folderTarget.Row;
            folderTarget.Column = 0;
            folderTarget.Row = 0;
            var folder = new TileItem
            {
                Name = "文件夹",
                IsTileFolder = true,
                Size = folderTarget.Size,
                Column = folderColumn,
                Row = folderRow,
                BackgroundColor = folderTarget.BackgroundColor,
                ForegroundColor = folderTarget.ForegroundColor,
                FolderTiles = [folderTarget],
            };
            var (childColumn, childRow) = TileFolderLayout.FindFirstAvailable(folder, _tile);
            _tile.Column = childColumn;
            _tile.Row = childRow;
            folder.FolderTiles.Add(_tile);
            TileFolderLayout.Normalize(folder);
            target.Tiles.Insert(0, folder);
            Intent = TileDropIntent.CreateFolder;
        }

        RefreshAffectedGroups(target);
        _previewTarget = target;
        _previewColumn = folderTarget.Column;
        _previewRow = folderTarget.Row;
        _previewFolderTargetId = folderTarget.Id;
        return true;
    }

    public bool PreviewInsideFolder(TileGroup target, TileItem folderTarget, int column, int row)
    {
        if (_tile.IsTileFolder
            || !folderTarget.IsTileFolder
            || !target.Tiles.Contains(folderTarget)
            || column < 0
            || row < 0
            || column + _tile.Size.ColumnSpan() > Win10TileMetrics.GroupColumns)
        {
            return false;
        }

        if (Intent == TileDropIntent.AddToFolder
            && _previewTarget == target
            && _previewFolderTargetId == folderTarget.Id
            && _previewColumn == column
            && _previewRow == row)
        {
            return true;
        }

        Restore();
        RemoveFromSource();
        if (!TileFolderLayout.AddOrMove(folderTarget, _tile, column, row))
        {
            return false;
        }

        RefreshAffectedGroups(target);
        _previewTarget = target;
        _previewColumn = column;
        _previewRow = row;
        _previewFolderTargetId = folderTarget.Id;
        Intent = TileDropIntent.AddToFolder;
        return true;
    }

    public TileGroup PreviewNewGroup()
    {
        if (_previewTarget is { } current && !_snapshots.Any(snapshot => snapshot.Group == current))
        {
            return current;
        }

        Restore();
        var group = TileGroupManager.Add(_layout);
        var moved = _sourceFolder is null
            ? Win10GroupLayout.Move(_source, group, _tile, 0, 0)
            : MoveFolderChildToGroup(group, 0, 0);
        if (!moved)
        {
            throw new InvalidOperationException("Unable to preview the tile in a new group.");
        }

        _previewTarget = group;
        _previewColumn = 0;
        _previewRow = 0;
        _previewFolderTargetId = null;
        Intent = TileDropIntent.NewGroup;
        return group;
    }

    public void Commit()
    {
        if (_sourceFolder is not null && _sourceFolder.FolderTiles.Count == 0)
        {
            _source.Tiles.Remove(_sourceFolder);
        }

        if (_previewTarget is not null && _previewTarget != _source && _source.Tiles.Count == 0)
        {
            _layout.Groups.Remove(_source);
        }

        foreach (var group in _layout.Groups)
        {
            group.RefreshLayout();
        }

        _committed = true;
    }

    public void Rollback()
    {
        if (!_committed)
        {
            Restore();
        }
    }

    public void Dispose()
    {
        Rollback();
    }

    private bool MoveFolderChildToGroup(TileGroup target, int column, int row)
    {
        RemoveFromSource();
        if (!Win10GroupLayout.Add(target, _tile, column, row))
        {
            return false;
        }

        RefreshAffectedGroups(target);
        return true;
    }

    private void RemoveFromSource()
    {
        if (_sourceFolder is null)
        {
            _source.Tiles.Remove(_tile);
        }
        else
        {
            _sourceFolder.FolderTiles.Remove(_tile);
            TileFolderLayout.Normalize(_sourceFolder);
            _source.RefreshLayout();
        }
    }

    private void RefreshAffectedGroups(TileGroup target)
    {
        Win10GroupLayout.Normalize(target);
        if (_source != target)
        {
            Win10GroupLayout.Normalize(_source);
        }
        else
        {
            _source.RefreshLayout();
        }
    }

    private static (int Column, int Row) FindFolderAppendPosition(TileItem folder, TileItem tile) =>
        TileFolderLayout.FindFirstAvailable(folder, tile);

    private void Restore()
    {
        foreach (var group in _layout.Groups.Where(group => !_snapshots.Any(snapshot => snapshot.Group == group)).ToArray())
        {
            _layout.Groups.Remove(group);
        }

        for (var index = 0; index < _snapshots.Length; index++)
        {
            var snapshot = _snapshots[index];
            var currentIndex = _layout.Groups.IndexOf(snapshot.Group);
            if (currentIndex < 0)
            {
                _layout.Groups.Insert(index, snapshot.Group);
            }
            else if (currentIndex != index)
            {
                _layout.Groups.Move(currentIndex, index);
            }

            snapshot.Restore();
        }

        _previewTarget = null;
        _previewColumn = -1;
        _previewRow = -1;
        _previewFolderTargetId = null;
        Intent = TileDropIntent.None;
    }

    private sealed record GroupSnapshot(TileGroup Group, TileSnapshot[] Tiles)
    {
        public static GroupSnapshot Capture(TileGroup group) =>
            new(group, group.Tiles.Select(TileSnapshot.Capture).ToArray());

        public void Restore()
        {
            Group.Tiles.Clear();
            foreach (var tile in Tiles)
            {
                tile.Restore();
                Group.Tiles.Add(tile.Tile);
            }

            Group.RefreshLayout();
        }
    }

    private sealed record TileSnapshot(
        TileItem Tile,
        int Column,
        int Row,
        bool IsTileFolder,
        bool IsFolderExpanded,
        TileSnapshot[] FolderTiles)
    {
        public static TileSnapshot Capture(TileItem tile) =>
            new(
                tile,
                tile.Column,
                tile.Row,
                tile.IsTileFolder,
                tile.IsFolderExpanded,
                tile.FolderTiles.Select(Capture).ToArray());

        public void Restore()
        {
            Tile.Column = Column;
            Tile.Row = Row;
            Tile.IsTileFolder = IsTileFolder;
            Tile.IsFolderExpanded = IsFolderExpanded;
            Tile.FolderTiles.Clear();
            foreach (var child in FolderTiles)
            {
                child.Restore();
                Tile.FolderTiles.Add(child.Tile);
            }
        }
    }
}
