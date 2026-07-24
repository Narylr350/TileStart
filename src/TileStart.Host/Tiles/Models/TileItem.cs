using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using TileStart.Host.Icons;

namespace TileStart.Host.Tiles.Models;

public sealed class TileItem : INotifyPropertyChanged
{
    private static readonly MediaBrush DefaultBackgroundBrush = CreateFrozenBrush("#3A3A3A");
    private int _column;
    private int _row;
    private TileSize _size;
    private string _name = string.Empty;
    private string _subtitle = string.Empty;
    private string _backgroundColor = "#3A3A3A";
    private string _foregroundColor = "#FFFFFF";
    private string _iconPath = string.Empty;
    private string _backgroundImagePath = string.Empty;
    private double _backgroundImageScale = 1;
    private bool _showTitle = true;
    private double _iconSize = 32;
    private TileIconPosition _iconPosition;
    private ImageSource? _icon;
    private ImageSource? _backgroundImage;
    private bool _usesFullTileLogo;
    private bool _isTileFolder;
    private ObservableCollection<TileItem> _folderTiles = [];
    private bool _isFolderExpanded;
    private bool _isDragging;
    private bool _isFolderDropTarget;
    private double _layoutOffsetY;
    private double _folderRegionTop;
    private double _folderRegionHeight;
    private double _folderContentHeight;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set
        {
            value ??= string.Empty;
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Initial));
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetText(ref _subtitle, value);
    }

    public string LaunchTarget { get; set; } = string.Empty;
    public TileTargetType TargetType { get; set; }
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;

    public string IconPath
    {
        get => _iconPath;
        set => SetText(ref _iconPath, value);
    }

    public CustomIconSourceKind IconSourceKind { get; set; }
    public string IconSourceValue { get; set; } = string.Empty;
    public bool RunAsAdministrator { get; set; }

    public bool IsTileFolder
    {
        get => _isTileFolder;
        set
        {
            if (_isTileFolder == value)
            {
                return;
            }

            _isTileFolder = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging == value)
            {
                return;
            }

            _isDragging = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsFolderDropTarget
    {
        get => _isFolderDropTarget;
        set
        {
            if (_isFolderDropTarget == value)
            {
                return;
            }

            _isFolderDropTarget = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsFolderExpanded
    {
        get => _isFolderExpanded;
        set
        {
            if (_isFolderExpanded == value)
            {
                return;
            }

            _isFolderExpanded = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TileItem> FolderTiles
    {
        get => _folderTiles;
        set
        {
            if (ReferenceEquals(_folderTiles, value))
            {
                return;
            }

            _folderTiles = value ?? [];
            OnPropertyChanged();
        }
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? "#3A3A3A" : value.Trim();
            if (_backgroundColor == value)
            {
                return;
            }

            _backgroundColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundBrush));
        }
    }

    public string ForegroundColor
    {
        get => _foregroundColor;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? "#FFFFFF" : value.Trim();
            if (_foregroundColor == value)
            {
                return;
            }

            _foregroundColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ForegroundBrush));
        }
    }

    public string BackgroundImagePath
    {
        get => _backgroundImagePath;
        set => SetText(ref _backgroundImagePath, value);
    }

    public double BackgroundImageScale
    {
        get => _backgroundImageScale;
        set
        {
            var normalized = double.IsFinite(value) ? Math.Clamp(value, 0.5, 3) : 1;
            if (_backgroundImageScale == normalized)
            {
                return;
            }

            _backgroundImageScale = normalized;
            OnPropertyChanged();
        }
    }

    public bool ShowTitle
    {
        get => _showTitle;
        set
        {
            if (_showTitle == value)
            {
                return;
            }

            _showTitle = value;
            OnPropertyChanged();
        }
    }

    public double IconSize
    {
        get => _iconSize;
        set
        {
            var normalized = double.IsFinite(value) ? Math.Clamp(value, 16, 204) : 32;
            if (_iconSize == normalized)
            {
                return;
            }

            _iconSize = normalized;
            OnPropertyChanged();
        }
    }

    public TileIconPosition IconPosition
    {
        get => _iconPosition;
        set
        {
            if (_iconPosition == value)
            {
                return;
            }

            _iconPosition = value;
            OnPropertyChanged();
        }
    }

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

    [JsonIgnore] public double Left => Win10TileMetrics.Left(Column);

    [JsonIgnore] public double Top => Win10TileMetrics.Top(Row);

    [JsonIgnore] public double DisplayTop => Top + _layoutOffsetY;

    [JsonIgnore] public double FolderRegionTop => _folderRegionTop;

    [JsonIgnore] public double FolderRegionHeight => _folderRegionHeight;

    [JsonIgnore] public double FolderContentHeight => _folderContentHeight;

    [JsonIgnore] public double PixelWidth => Win10TileMetrics.Width(Size);

    [JsonIgnore] public double PixelHeight => Win10TileMetrics.Height(Size);

    [JsonIgnore]
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[0].ToString().ToUpperInvariant();

    [JsonIgnore] public MediaBrush BackgroundBrush => ParseBrush(BackgroundColor, DefaultBackgroundBrush);

    [JsonIgnore] public MediaBrush ForegroundBrush => ParseBrush(ForegroundColor, MediaBrushes.White);

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

    [JsonIgnore]
    public bool UsesFullTileLogo
    {
        get => _usesFullTileLogo;
        set
        {
            if (_usesFullTileLogo == value)
            {
                return;
            }

            _usesFullTileLogo = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public ImageSource? BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            if (ReferenceEquals(_backgroundImage, value))
            {
                return;
            }

            _backgroundImage = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetLayoutOffset(double offsetY)
    {
        if (_layoutOffsetY == offsetY)
        {
            return;
        }

        _layoutOffsetY = offsetY;
        OnPropertyChanged(nameof(DisplayTop));
    }

    internal void SetFolderRegionLayout(double top, double height, double contentHeight)
    {
        if (_folderRegionTop != top)
        {
            _folderRegionTop = top;
            OnPropertyChanged(nameof(FolderRegionTop));
        }

        if (_folderRegionHeight != height)
        {
            _folderRegionHeight = height;
            OnPropertyChanged(nameof(FolderRegionHeight));
        }

        if (_folderContentHeight != contentHeight)
        {
            _folderContentHeight = contentHeight;
            OnPropertyChanged(nameof(FolderContentHeight));
        }
    }

    private static MediaBrush CreateFrozenBrush(string value)
    {
        var brush = (MediaBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }

    private static MediaBrush ParseBrush(string value, MediaBrush fallback)
    {
        try
        {
            var brush = (MediaBrush?)new BrushConverter().ConvertFromString(value);
            if (brush is not null && brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush ?? fallback;
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            return fallback;
        }
    }

    private void SetText(ref string field, string? value, [CallerMemberName] string? propertyName = null)
    {
        value ??= string.Empty;
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void NotifyLayoutChanged()
    {
        OnPropertyChanged();
        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Top));
        OnPropertyChanged(nameof(DisplayTop));
        OnPropertyChanged(nameof(PixelWidth));
        OnPropertyChanged(nameof(PixelHeight));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}