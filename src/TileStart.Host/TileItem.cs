using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace TileStart.Host;

public sealed class TileItem : INotifyPropertyChanged
{
    private int _column;
    private int _row;
    private TileSize _size;
    private string _name = string.Empty;
    private ImageSource? _icon;

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
            OnPropertyChanged(nameof(Initial));
        }
    }

    public string LaunchTarget { get; set; } = string.Empty;
    public TileTargetType TargetType { get; set; }
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public bool RunAsAdministrator { get; set; }

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
    public double Left => Win10TileMetrics.Left(Column);

    [JsonIgnore]
    public double Top => Win10TileMetrics.Top(Row);

    [JsonIgnore]
    public double PixelWidth => Win10TileMetrics.Width(Size);

    [JsonIgnore]
    public double PixelHeight => Win10TileMetrics.Height(Size);

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
