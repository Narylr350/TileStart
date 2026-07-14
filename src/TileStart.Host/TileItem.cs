using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace TileStart.Host;

public sealed class TileItem : INotifyPropertyChanged
{
    public const double CellSize = 48;
    public const double Gap = 4;
    public const double CellPitch = CellSize + Gap;

    private int _column;
    private int _row;
    private TileSize _size;
    private ImageSource? _icon;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string LaunchTarget { get; set; } = string.Empty;

    public TileSize Size
    {
        get => _size;
        set
        {
            if (_size == value)
            {
                return;
            }

            _size = value;
            NotifyLayoutChanged();
        }
    }

    public int Column
    {
        get => _column;
        set
        {
            if (_column == value)
            {
                return;
            }

            _column = value;
            NotifyLayoutChanged();
        }
    }

    public int Row
    {
        get => _row;
        set
        {
            if (_row == value)
            {
                return;
            }

            _row = value;
            NotifyLayoutChanged();
        }
    }

    [JsonIgnore]
    public double Left => Column * CellPitch;

    [JsonIgnore]
    public double Top => Row * CellPitch;

    [JsonIgnore]
    public double PixelWidth => Size.ColumnSpan() * CellPitch - Gap;

    [JsonIgnore]
    public double PixelHeight => Size.RowSpan() * CellPitch - Gap;

    [JsonIgnore]
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[0].ToString().ToUpperInvariant();

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyLayoutChanged()
    {
        OnPropertyChanged();
        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Top));
        OnPropertyChanged(nameof(PixelWidth));
        OnPropertyChanged(nameof(PixelHeight));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
