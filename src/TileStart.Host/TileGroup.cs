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
            TrackTiles();
            OnPropertyChanged();
            OnPropertyChanged(nameof(PixelHeight));
        }
    }

    [JsonIgnore]
    public double PixelHeight
    {
        get
        {
            var rows = Tiles.Count == 0 ? 1 : Tiles.Max(tile => tile.Row + tile.Size.RowSpan());
            return rows * Win10TileMetrics.CellPitch - Win10TileMetrics.Gap;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLayout()
    {
        TrackTiles();
        OnPropertyChanged(nameof(PixelHeight));
    }

    private void Tiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        TrackTiles();
        OnPropertyChanged(nameof(PixelHeight));
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
        if (e.PropertyName is nameof(TileItem.Row) or nameof(TileItem.Size))
        {
            OnPropertyChanged(nameof(PixelHeight));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
