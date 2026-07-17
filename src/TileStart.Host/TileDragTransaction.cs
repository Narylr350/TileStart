namespace TileStart.Host;

public sealed class TileDragTransaction : IDisposable
{
    private readonly TileLayout _layout;
    private readonly TileGroup _source;
    private readonly TileItem _tile;
    private readonly GroupSnapshot[] _snapshots;
    private TileGroup? _previewTarget;
    private int _previewColumn = -1;
    private int _previewRow = -1;
    private string? _previewFolderTargetId;
    private bool _committed;

    public TileDragTransaction(TileLayout layout, TileGroup source, TileItem tile)
    {
        if (!source.Tiles.Contains(tile))
        {
            throw new ArgumentException("The source group does not contain the tile.", nameof(tile));
        }

        _layout = layout;
        _source = source;
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
        if (!Win10GroupLayout.Move(_source, target, _tile, column, row))
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

        _source.Tiles.Remove(_tile);
        if (folderTarget.IsTileFolder)
        {
            folderTarget.FolderTiles.Add(_tile);
            Intent = TileDropIntent.AddToFolder;
        }
        else
        {
            target.Tiles.Remove(folderTarget);
            var folder = new TileItem
            {
                Name = "文件夹",
                IsTileFolder = true,
                Size = folderTarget.Size,
                Column = folderTarget.Column,
                Row = folderTarget.Row,
                BackgroundColor = folderTarget.BackgroundColor,
                ForegroundColor = folderTarget.ForegroundColor,
                FolderTiles = [folderTarget, _tile],
            };
            target.Tiles.Insert(0, folder);
            Intent = TileDropIntent.CreateFolder;
        }

        Win10GroupLayout.Normalize(target);
        if (_source != target)
        {
            Win10GroupLayout.Normalize(_source);
        }

        _previewTarget = target;
        _previewColumn = folderTarget.Column;
        _previewRow = folderTarget.Row;
        _previewFolderTargetId = folderTarget.Id;
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
        if (!Win10GroupLayout.Move(_source, group, _tile, 0, 0))
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
        if (_previewTarget is not null && _previewTarget != _source && _source.Tiles.Count == 0)
        {
            _layout.Groups.Remove(_source);
        }

        _committed = true;
    }

    public void Rollback()
    {
        if (_committed)
        {
            return;
        }

        Restore();
    }

    public void Dispose()
    {
        Rollback();
    }

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

    private sealed record TileSnapshot(TileItem Tile, int Column, int Row, bool IsTileFolder, TileItem[] FolderTiles)
    {
        public static TileSnapshot Capture(TileItem tile) =>
            new(tile, tile.Column, tile.Row, tile.IsTileFolder, tile.FolderTiles.ToArray());

        public void Restore()
        {
            Tile.Column = Column;
            Tile.Row = Row;
            Tile.IsTileFolder = IsTileFolder;
            Tile.FolderTiles.Clear();
            foreach (var child in FolderTiles)
            {
                Tile.FolderTiles.Add(child);
            }
        }
    }
}
