using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TileStart.Host.Tiles.Layout;

namespace TileStart.Host.Tiles.Models;

public sealed class TileGroup : INotifyPropertyChanged
{
    private readonly HashSet<TileItem> _trackedTiles = [];
    private int _groupColumn = -1;
    private int _groupRow = -1;
    private int _widthUnits = TileWorkspaceMetrics.LegacyGroupWidthUnits;
    private int _heightUnits;
    private string _name = string.Empty;
    private ObservableCollection<TileItem> _tiles = [];

    public TileGroup()
    {
        _tiles.CollectionChanged += Tiles_CollectionChanged;
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public int GroupColumn
    {
        get => _groupColumn;
        set
        {
            if (_groupColumn == value)
            {
                return;
            }

            _groupColumn = value;
            OnPropertyChanged();
        }
    }

    public int GroupRow
    {
        get => _groupRow;
        set
        {
            if (_groupRow == value)
            {
                return;
            }

            _groupRow = value;
            OnPropertyChanged();
        }
    }

    public int WidthUnits
    {
        get => _widthUnits;
        set
        {
            value = Math.Clamp(
                value,
                TileWorkspaceMetrics.MinimumGroupWidthUnits,
                TileWorkspaceMetrics.MaximumGroupWidthUnits);
            if (_widthUnits == value)
            {
                return;
            }

            _widthUnits = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContentColumns));
            OnPropertyChanged(nameof(PixelWidth));
            OnPropertyChanged(nameof(VisualWidth));
            OnPropertyChanged(nameof(TileCanvasHorizontalInset));
        }
    }

    public int HeightUnits
    {
        get => _heightUnits;
        set
        {
            value = Math.Clamp(value, 0, TileWorkspaceMetrics.MaximumGroupHeightUnits);
            if (_heightUnits == value)
            {
                return;
            }

            _heightUnits = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContentRowLimit));
            RefreshLayout();
        }
    }

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

    [JsonIgnore] public int ContentColumns => TileWorkspaceMetrics.TileColumns(WidthUnits);

    [JsonIgnore] public int? ContentRowLimit => HeightUnits == 0 ? null : TileWorkspaceMetrics.TileRows(HeightUnits);

    [JsonIgnore] public double PixelWidth => Win10TileMetrics.WidthForColumns(ContentColumns);

    [JsonIgnore] public double VisualWidth => TileWorkspaceMetrics.GroupVisualWidth(WidthUnits);

    [JsonIgnore] public double TileCanvasHorizontalInset => Math.Max(0, (VisualWidth - PixelWidth) / 2);

    [JsonIgnore]
    public double PixelHeight
    {
        get
        {
            var minimumHeight = ContentRowLimit is { } rows
                ? Win10TileMetrics.HeightForRows(rows)
                : 0;
            var tileBottom = Tiles.Count == 0
                ? Win10TileMetrics.CellSize
                : Tiles.Max(tile => tile.DisplayTop + tile.PixelHeight);
            var regionBottom = Tiles.Where(tile => tile.IsFolderExpanded)
                .Select(tile => tile.FolderRegionTop + tile.FolderRegionHeight)
                .DefaultIfEmpty(0)
                .Max();
            return Math.Max(minimumHeight, Math.Max(tileBottom, regionBottom));
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