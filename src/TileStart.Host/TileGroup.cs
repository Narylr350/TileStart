using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TileStart.Host;

public sealed class TileGroup : INotifyPropertyChanged
{
    public const double PixelWidth = Win10TileMetrics.GroupWidth;

    private readonly HashSet<TileItem> _trackedTiles = [];
    private string _name = string.Empty;
    private ObservableCollection<TileItem> _tiles = [];

    public TileGroup()
    {
        _tiles.CollectionChanged += Tiles_CollectionChanged;
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TileItem> Tiles
    {
        get => _tiles;
        set
        {
            _tiles.CollectionChanged -= Tiles_CollectionChanged;
            _tiles = value ?? [];
            _tiles.CollectionChanged += Tiles_CollectionChanged;
            RefreshLayout();
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public double PixelHeight
    {
        get
        {
            var tileBottom = Tiles.Count == 0
                ? Win10TileMetrics.CellSize
                : Tiles.Max(tile => tile.DisplayTop + tile.PixelHeight);
            var regionBottom = Tiles.Where(tile => tile.IsFolderExpanded)
                .Select(tile => tile.FolderRegionTop + tile.FolderRegionHeight)
                .DefaultIfEmpty(0)
                .Max();
            return Math.Max(tileBottom, regionBottom);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLayout()
    {
        TrackTiles();
        UpdateFolderRegions();
        OnPropertyChanged(nameof(PixelHeight));
    }

    private void UpdateFolderRegions()
    {
        var regions = Tiles
            .Where(tile => tile.IsTileFolder && tile.IsFolderExpanded)
            .Select(tile => new
            {
                Tile = tile,
                InsertionRow = tile.Row + tile.Size.RowSpan(),
                Height = TileFolderLayout.RegionHeight(tile),
                ContentHeight = TileFolderLayout.ContentHeight(tile),
            })
            .OrderBy(region => region.InsertionRow)
            .ThenBy(region => region.Tile.Column)
            .ToArray();

        foreach (var tile in Tiles)
        {
            var offset = regions
                .Where(region => tile.Row >= region.InsertionRow)
                .Sum(region => region.Height);
            tile.SetLayoutOffset(offset);
            if (!tile.IsFolderExpanded)
            {
                tile.SetFolderRegionLayout(0, 0, 0);
            }
        }

        var precedingHeight = 0d;
        foreach (var region in regions)
        {
            var top = Win10TileMetrics.Top(region.InsertionRow) + precedingHeight;
            region.Tile.SetFolderRegionLayout(top, region.Height, region.ContentHeight);
            precedingHeight += region.Height;
        }
    }

    private void Tiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshLayout();
    }

    private void TrackTiles()
    {
        foreach (var tile in _trackedTiles.Except(Tiles).ToArray())
        {
            tile.PropertyChanged -= Tile_PropertyChanged;
            _trackedTiles.Remove(tile);
        }

        foreach (var tile in Tiles.Where(tile => _trackedTiles.Add(tile)))
        {
            tile.PropertyChanged += Tile_PropertyChanged;
        }
    }

    private void Tile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TileItem.Row)
            or nameof(TileItem.Size)
            or nameof(TileItem.IsFolderExpanded))
        {
            RefreshLayout();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
