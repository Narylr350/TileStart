using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TileStart.Host;

public sealed class GroupTileOption : INotifyPropertyChanged
{
    private bool _isSelected;
    private ImageSource? _icon;
    private AppEntry? _app;

    public required string Key { get; init; }
    public required string Name { get; init; }

    public required ImageSource? Icon
    {
        get => _app?.Icon ?? _icon ?? GenericAppIcon.Image;
        init => _icon = value;
    }

    public TileItem? ExistingTile { get; init; }

    public AppEntry? App
    {
        get => _app;
        init
        {
            _app = value;
            if (_app is not null)
            {
                _app.PropertyChanged += App_PropertyChanged;
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Detach()
    {
        if (_app is not null)
        {
            _app.PropertyChanged -= App_PropertyChanged;
        }
    }

    private void App_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppEntry.Icon))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }
}

public partial class GroupSettingsWindow : Window, INotifyPropertyChanged
{
    private readonly ObservableCollection<GroupTileOption> _options;
    private readonly ICollectionView _optionsView;
    private bool _isReady;
    private TileGroup _previewGroup = new();
    private string _validationMessage = string.Empty;
    private bool _showSelectedOnly;

    public GroupSettingsWindow(TileGroup group, IReadOnlyList<AppEntry> apps)
    {
        _options = CreateOptions(group, apps);
        foreach (var option in _options)
        {
            option.PropertyChanged += Option_PropertyChanged;
        }

        _optionsView = CollectionViewSource.GetDefaultView(_options);
        _optionsView.Filter = FilterOption;
        InitializeComponent();
        DataContext = this;
        NameBox.Text = group.Name;
        WidthBox.SelectedValue = group.WidthUnits.ToString();
        HeightBox.SelectedValue = group.HeightUnits.ToString();
        _isReady = true;
        RefreshPreview();
    }

    public ICollectionView TileOptionsView => _optionsView;

    public TileGroup PreviewGroup
    {
        get => _previewGroup;
        private set
        {
            _previewGroup = value;
            OnPropertyChanged();
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (_validationMessage == value)
            {
                return;
            }

            _validationMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationMessage));
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
    public string GroupName => NameBox.Text.Trim();
    public int WidthUnits => ParseSelectedUnits(WidthBox, TileWorkspaceMetrics.LegacyGroupWidthUnits);
    public int HeightUnits => ParseSelectedUnits(HeightBox, 0);
    public IReadOnlyList<GroupTileOption> SelectedOptions => [.. _options.Where(option => option.IsSelected)];
    public string SelectedCountText => $"已选择 {SelectedOptions.Count} 项";

    public event PropertyChangedEventHandler? PropertyChanged;

    private static ObservableCollection<GroupTileOption> CreateOptions(
        TileGroup group,
        IReadOnlyList<AppEntry> apps)
    {
        var options = new List<GroupTileOption>();
        var existingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tile in group.Tiles.OrderBy(tile => tile.Row).ThenBy(tile => tile.Column))
        {
            var key = TileKey(tile.LaunchTarget, tile.Id);
            if (!string.IsNullOrWhiteSpace(tile.LaunchTarget))
            {
                existingTargets.Add(tile.LaunchTarget);
            }

            options.Add(new GroupTileOption
            {
                Key = key,
                Name = tile.Name,
                Icon = ResolveOptionIcon(tile.Icon, tile.LaunchTarget, tile.UsesFullTileLogo),
                ExistingTile = tile,
                IsSelected = true,
            });
        }

        foreach (var app in AppEntry.FlattenApplications(apps)
                     .Where(app => !string.IsNullOrWhiteSpace(app.LaunchTarget))
                     .GroupBy(app => app.LaunchTarget, StringComparer.OrdinalIgnoreCase)
                     .Select(grouping => grouping.First())
                     .Where(app => !existingTargets.Contains(app.LaunchTarget))
                     .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            options.Add(new GroupTileOption
            {
                Key = TileKey(app.LaunchTarget, app.Name),
                Name = app.Name,
                Icon = app.Icon,
                App = app,
            });
        }

        return new ObservableCollection<GroupTileOption>(options);
    }

    private static ImageSource ResolveOptionIcon(ImageSource? currentIcon, string launchTarget, bool usesFullTileLogo)
    {
        if (!usesFullTileLogo && currentIcon is not null)
        {
            return currentIcon;
        }

        return ShellIconLoader.Load(launchTarget) ?? currentIcon ?? GenericAppIcon.Image;
    }

    private static string TileKey(string launchTarget, string fallback) =>
        string.IsNullOrWhiteSpace(launchTarget) ? fallback : launchTarget;

    private void Option_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupTileOption.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCountText));
            if (_showSelectedOnly)
            {
                _optionsView.Refresh();
            }

            RefreshPreview();
        }
    }

    private bool FilterOption(object item)
    {
        if (item is not GroupTileOption option)
        {
            return false;
        }

        var query = SearchBox?.Text.Trim();
        return (!_showSelectedOnly || option.IsSelected)
               && (string.IsNullOrWhiteSpace(query)
                   || option.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _optionsView.Refresh();
    }

    private void SelectedOnly_Changed(object sender, RoutedEventArgs e)
    {
        _showSelectedOnly = SelectedOnlyBox.IsChecked == true;
        _optionsView.Refresh();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var option in _options.Where(option => option.IsSelected))
        {
            option.IsSelected = false;
        }
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
    }

    private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (!_isReady)
        {
            return;
        }

        var preview = new TileGroup
        {
            Name = string.IsNullOrWhiteSpace(GroupName) ? "命名组" : GroupName,
            WidthUnits = WidthUnits,
            HeightUnits = HeightUnits,
            Tiles = [.. SelectedOptions.Select(option => CloneForPreview(option))],
        };
        var fits = Win10GroupLayout.Normalize(preview);
        PreviewGroup = preview;
        ValidationMessage = fits
            ? string.Empty
            : $"所选磁贴无法放入 {WidthUnits}×{HeightUnits} 的组，请增大尺寸或减少磁贴。";
        SaveButton.IsEnabled = fits && !string.IsNullOrWhiteSpace(GroupName);
        PreviewSizeText.Text = HeightUnits == 0
            ? $"{WidthUnits}×自动 · {preview.PixelWidth:0} DIP 宽"
            : $"{WidthUnits}×{HeightUnits} · {preview.PixelWidth:0} × {preview.PixelHeight:0} DIP";
    }

    private static TileItem CloneForPreview(GroupTileOption option)
    {
        if (option.ExistingTile is { } tile)
        {
            return CloneTile(tile);
        }

        var app = option.App!;
        return new TileItem
        {
            Name = app.Name,
            LaunchTarget = app.LaunchTarget,
            TargetType = TileTargetType.Application,
            Size = TileSize.Medium,
            Icon = option.Icon,
        };
    }

    private static TileItem CloneTile(TileItem tile)
    {
        return new TileItem
        {
            Name = tile.Name,
            Subtitle = tile.Subtitle,
            LaunchTarget = tile.LaunchTarget,
            TargetType = tile.TargetType,
            Arguments = tile.Arguments,
            WorkingDirectory = tile.WorkingDirectory,
            IconPath = tile.IconPath,
            IconSourceKind = tile.IconSourceKind,
            IconSourceValue = tile.IconSourceValue,
            RunAsAdministrator = tile.RunAsAdministrator,
            IsTileFolder = tile.IsTileFolder,
            FolderTiles = [.. tile.FolderTiles.Select(CloneTile)],
            BackgroundColor = tile.BackgroundColor,
            ForegroundColor = tile.ForegroundColor,
            BackgroundImagePath = tile.BackgroundImagePath,
            ShowTitle = tile.ShowTitle,
            IconSize = tile.IconSize,
            IconPosition = tile.IconPosition,
            Size = tile.Size,
            Icon = tile.Icon,
            BackgroundImage = tile.BackgroundImage,
            UsesFullTileLogo = tile.UsesFullTileLogo,
        };
    }

    private static int ParseSelectedUnits(System.Windows.Controls.ComboBox comboBox, int fallback) =>
        int.TryParse(comboBox.SelectedValue as string, out var value) ? value : fallback;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
        if (!SaveButton.IsEnabled)
        {
            return;
        }

        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            DialogSurface.Opacity = 1;
            DialogSurface.RenderTransform = Transform.Identity;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(180);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        DialogSurface.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = easing,
        });
        if (DialogSurface.RenderTransform is not TransformGroup { Children.Count: >= 2 } transforms ||
            transforms.Children[0] is not ScaleTransform scale ||
            transforms.Children[1] is not TranslateTransform translate)
        {
            return;
        }

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, duration)
        {
            EasingFunction = easing,
        });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, duration)
        {
            EasingFunction = easing,
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, duration)
        {
            EasingFunction = easing,
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var option in _options)
        {
            option.Detach();
        }

        base.OnClosed(e);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}