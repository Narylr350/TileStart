using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorDialog = System.Windows.Forms.ColorDialog;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushConverter = System.Windows.Media.BrushConverter;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace TileStart.Host;

public partial class TileSettingsWindow : Window
{
    private readonly ImageSource? _defaultIcon;
    private readonly bool _defaultUsesFullTileLogo;
    private readonly string _subtitle;
    private readonly TileTargetType _targetType;
    private CustomIconSourceKind _iconSourceKind;
    private string _iconSourceValue;
    private TileSize _previewSize;
    private bool _isReady;

    public TileSettingsWindow(TileItem tile, bool isNew = false)
    {
        _subtitle = tile.Subtitle;
        _targetType = tile.TargetType;
        _previewSize = tile.Size;
        _iconSourceKind = tile.IconSourceKind == CustomIconSourceKind.Default && !string.IsNullOrWhiteSpace(tile.IconPath)
            ? CustomIconSourceKind.LocalFile
            : tile.IconSourceKind;
        _iconSourceValue = string.IsNullOrWhiteSpace(tile.IconSourceValue) ? tile.IconPath : tile.IconSourceValue;
        _defaultIcon = string.IsNullOrWhiteSpace(tile.IconPath)
            ? tile.Icon
            : ShellIconLoader.Load(tile.LaunchTarget) ?? tile.Icon;
        _defaultUsesFullTileLogo = string.IsNullOrWhiteSpace(tile.IconPath) && tile.UsesFullTileLogo;
        PreviewTile = new TileItem
        {
            Name = tile.Name,
            Subtitle = tile.Subtitle,
            LaunchTarget = tile.LaunchTarget,
            TargetType = tile.TargetType,
            Size = tile.Size,
            BackgroundColor = tile.BackgroundColor,
            ForegroundColor = tile.ForegroundColor,
            BackgroundImagePath = tile.BackgroundImagePath,
            BackgroundImage = tile.BackgroundImage,
            ShowTitle = tile.ShowTitle,
            IconSize = tile.IconSize,
            IconPosition = tile.IconPosition,
            Icon = tile.Icon,
            UsesFullTileLogo = tile.UsesFullTileLogo,
            IsTileFolder = tile.IsTileFolder,
            FolderTiles = tile.FolderTiles,
        };
        InitializeComponent();
        DataContext = this;
        NameBox.Text = tile.Name;
        TargetBox.Text = tile.LaunchTarget;
        ArgumentsBox.Text = tile.Arguments;
        WorkingDirectoryBox.Text = tile.WorkingDirectory;
        IconPathBox.Text = tile.IconPath;
        BackgroundImagePathBox.Text = tile.BackgroundImagePath;
        BackgroundColorBox.Text = tile.BackgroundColor;
        ForegroundColorBox.Text = tile.ForegroundColor;
        ShowTitleBox.IsChecked = tile.ShowTitle;
        IconSizeBox.Maximum = Win10TileMetrics.MaxIconSize(tile.Size);
        IconSizeBox.Value = tile.IconSize;
        IconPositionBox.SelectedValue = tile.IconPosition.ToString();
        SizeBox.SelectedValue = tile.Size.ToString();
        RunAsAdministratorBox.IsChecked = tile.RunAsAdministrator;

        var hasLaunchTarget = !tile.IsTileFolder;
        var canEditTarget = hasLaunchTarget && (isNew || tile.TargetType == TileTargetType.Command);
        LaunchSection.Visibility = hasLaunchTarget ? Visibility.Visible : Visibility.Collapsed;
        TargetBox.IsReadOnly = !canEditTarget;
        BrowseTargetButton.Visibility = canEditTarget ? Visibility.Visible : Visibility.Collapsed;
        TargetHint.Visibility = canEditTarget ? Visibility.Collapsed : Visibility.Visible;
        UnpinButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;

        UpdateColorPreview(BackgroundColorPreview, BackgroundColor);
        UpdateColorPreview(ForegroundColorPreview, ForegroundColor);
        _isReady = true;
        RefreshPreview();
    }

    public TileItem PreviewTile { get; }

    public string TileName => NameBox.Text.Trim();
    public string Subtitle => _subtitle;
    public string LaunchTarget => TargetBox.Text.Trim();
    public string Arguments => ArgumentsBox.Text.Trim();
    public string WorkingDirectory => WorkingDirectoryBox.Text.Trim();
    public string IconPath => IconPathBox.Text.Trim();
    public CustomIconSourceKind IconSourceKind => _iconSourceKind;
    public string IconSourceValue => _iconSourceValue;
    public string BackgroundImagePath => BackgroundImagePathBox.Text.Trim();
    public string BackgroundColor => BackgroundColorBox.Text.Trim();
    public string ForegroundColor => ForegroundColorBox.Text.Trim();
    public bool ShowTitle => ShowTitleBox.IsChecked == true;
    public double IconSize => IconSizeBox.Value;
    public TileIconPosition IconPosition => Enum.Parse<TileIconPosition>((string)IconPositionBox.SelectedValue);
    public TileSize TileSize => Enum.Parse<TileSize>((string)SizeBox.SelectedValue);
    public bool RunAsAdministrator => RunAsAdministratorBox.IsChecked == true;
    public bool ShouldUnpin { get; private set; }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "可启动项目|*.exe;*.lnk;*.appref-ms;*.bat;*.cmd;*.ps1;*.url|所有文件|*.*",
            FileName = File.Exists(LaunchTarget) ? LaunchTarget : string.Empty,
        };
        if (dialog.ShowDialog(this) == true)
        {
            TargetBox.Text = dialog.FileName;
        }
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(WorkingDirectory) ? WorkingDirectory : string.Empty,
            Title = "选择工作目录",
        };
        if (dialog.ShowDialog(this) == true)
        {
            WorkingDirectoryBox.Text = dialog.FolderName;
        }
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "图标来源|*.ico;*.exe;*.dll;*.png;*.jpg;*.jpeg;*.bmp;*.svg|所有文件|*.*",
            FileName = File.Exists(IconPath) ? IconPath : string.Empty,
        };
        if (dialog.ShowDialog(this) == true)
        {
            IconPathBox.Text = dialog.FileName;
            _iconSourceKind = CustomIconSourceKind.LocalFile;
            _iconSourceValue = dialog.FileName;
            RefreshPreview();
        }
    }

    private void ChooseNetworkIcon_Click(object sender, RoutedEventArgs e)
    {
        var sourceUrl = _iconSourceKind == CustomIconSourceKind.Network ? _iconSourceValue : string.Empty;
        var dialog = new NetworkIconWindow(sourceUrl) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _iconSourceKind = CustomIconSourceKind.Network;
            _iconSourceValue = dialog.SourceUrl;
            IconPathBox.Text = dialog.IconPath;
            RefreshPreview();
        }
    }

    private void ChooseSvgIcon_Click(object sender, RoutedEventArgs e)
    {
        var source = string.Empty;
        if (_iconSourceKind == CustomIconSourceKind.Svg && File.Exists(_iconSourceValue))
        {
            source = File.ReadAllText(_iconSourceValue);
        }

        var dialog = new SvgIconWindow(source) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _iconSourceKind = CustomIconSourceKind.Svg;
            _iconSourceValue = dialog.IconPath;
            IconPathBox.Text = dialog.IconPath;
            RefreshPreview();
        }
    }

    private void ClearIcon_Click(object sender, RoutedEventArgs e)
    {
        _iconSourceKind = CustomIconSourceKind.Default;
        _iconSourceValue = string.Empty;
        IconPathBox.Clear();
        RefreshPreview();
    }

    private void RestoreDefaultAppearance_Click(object sender, RoutedEventArgs e)
    {
        IconPathBox.Clear();
        BackgroundImagePathBox.Clear();
        BackgroundColorBox.Text = "#3A3A3A";
        ForegroundColorBox.Text = "#FFFFFF";
        ShowTitleBox.IsChecked = true;
        IconSizeBox.Value = 32;
        IconPositionBox.SelectedValue = TileIconPosition.Center.ToString();
        _iconSourceKind = CustomIconSourceKind.Default;
        _iconSourceValue = string.Empty;
        RefreshPreview();
    }

    private void TestLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LaunchTarget))
        {
            MessageBox.Show(this, "请先填写启动目标。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var testTile = new TileItem
        {
            Name = string.IsNullOrWhiteSpace(TileName) ? "测试磁贴" : TileName,
            LaunchTarget = LaunchTarget,
            TargetType = _targetType,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            RunAsAdministrator = RunAsAdministrator,
        };
        if (!AppLauncher.Launch(testTile))
        {
            MessageBox.Show(this, "启动失败，请检查目标、参数和工作目录。", "TileStart", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BrowseBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
            FileName = File.Exists(BackgroundImagePath) ? BackgroundImagePath : string.Empty,
        };
        if (dialog.ShowDialog(this) == true)
        {
            BackgroundImagePathBox.Text = dialog.FileName;
        }
    }

    private void ClearBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        BackgroundImagePathBox.Clear();
    }

    private void ChooseBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        ChooseColor(BackgroundColorBox);
    }

    private void ChooseForegroundColor_Click(object sender, RoutedEventArgs e)
    {
        ChooseColor(ForegroundColorBox);
    }

    private static void ChooseColor(System.Windows.Controls.TextBox target)
    {
        using var dialog = new ColorDialog { FullOpen = true };
        if (dialog.ShowDialog() == FormsDialogResult.OK)
        {
            target.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void BackgroundColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview(BackgroundColorPreview, BackgroundColorBox.Text);
        RefreshPreview();
    }

    private void ForegroundColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview(ForegroundColorPreview, ForegroundColorBox.Text);
        RefreshPreview();
    }

    private void PreviewTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshPreview();
    }

    private void PreviewOption_Changed(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
    }

    private void PreviewSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isReady
            && ReferenceEquals(sender, SizeBox)
            && Enum.TryParse<TileSize>(SizeBox.SelectedValue as string, out var selectedSize)
            && selectedSize != _previewSize)
        {
            var scaledIconSize = Win10TileMetrics.ScaleIconSize(IconSizeBox.Value, _previewSize, selectedSize);
            _previewSize = selectedSize;
            IconSizeBox.Maximum = Win10TileMetrics.MaxIconSize(selectedSize);
            IconSizeBox.Value = scaledIconSize;
        }

        RefreshPreview();
    }

    private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (!_isReady)
        {
            return;
        }

        PreviewTile.Name = string.IsNullOrWhiteSpace(TileName) ? "磁贴名称" : TileName;
        PreviewTile.BackgroundColor = IsValidColor(BackgroundColor) ? BackgroundColor : "#3A3A3A";
        PreviewTile.ForegroundColor = IsValidColor(ForegroundColor) ? ForegroundColor : "#FFFFFF";
        PreviewTile.BackgroundImagePath = BackgroundImagePath;
        PreviewTile.BackgroundImage = ShellIconLoader.LoadImage(BackgroundImagePath);
        PreviewTile.ShowTitle = ShowTitle;
        PreviewTile.IconSize = IconSize;
        PreviewTile.IconPosition = Enum.TryParse<TileIconPosition>(IconPositionBox.SelectedValue as string, out var iconPosition)
            ? iconPosition
            : TileIconPosition.Center;
        PreviewTile.Size = Enum.TryParse<TileSize>(SizeBox.SelectedValue as string, out var size)
            ? size
            : TileSize.Medium;

        if (string.IsNullOrWhiteSpace(IconPath))
        {
            PreviewTile.Icon = _defaultIcon;
            PreviewTile.UsesFullTileLogo = _defaultUsesFullTileLogo;
        }
        else
        {
            PreviewTile.Icon = File.Exists(IconPath) ? ShellIconLoader.Load(IconPath) : null;
            PreviewTile.UsesFullTileLogo = false;
        }

        PreviewSizeText.Text = $"{TileSizeLabel(PreviewTile.Size)} · {PreviewTile.PixelWidth:0} × {PreviewTile.PixelHeight:0} DIP";
        IconSourceText.Text = _iconSourceKind switch
        {
            CustomIconSourceKind.Network => $"网络 · {_iconSourceValue}",
            CustomIconSourceKind.Svg => "SVG 源码图标",
            CustomIconSourceKind.LocalFile => $"本地 · {Path.GetFileName(IconPath)}",
            _ => "应用默认图标",
        };
    }

    private static string TileSizeLabel(TileSize size) => size switch
    {
        TileSize.Small => "小",
        TileSize.Medium => "中",
        TileSize.Wide => "宽",
        TileSize.Large => "大",
        _ => "中",
    };

    private static void UpdateColorPreview(Border preview, string value)
    {
        try
        {
            var brush = (MediaBrush?)new MediaBrushConverter().ConvertFromString(value);
            if (brush?.CanFreeze == true)
            {
                brush.Freeze();
            }

            preview.Background = brush ?? MediaBrushes.Transparent;
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            preview.Background = MediaBrushes.Transparent;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TileName))
        {
            MessageBox.Show(this, "磁贴名称不能为空。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (LaunchSection.Visibility == Visibility.Visible && string.IsNullOrWhiteSpace(LaunchTarget))
        {
            MessageBox.Show(this, "命令或可执行文件不能为空。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
        {
            MessageBox.Show(this, "工作目录不存在。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(IconPath) && !File.Exists(IconPath))
        {
            MessageBox.Show(this, "图标来源不存在。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(BackgroundImagePath) && !File.Exists(BackgroundImagePath))
        {
            MessageBox.Show(this, "背景图片不存在。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!IsValidColor(BackgroundColor) || !IsValidColor(ForegroundColor))
        {
            MessageBox.Show(this, "颜色必须是 #RRGGBB、#AARRGGBB 或 WPF 颜色名称。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private static bool IsValidColor(string value)
    {
        try
        {
            return MediaColorConverter.ConvertFromString(value) is not null;
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            return false;
        }
    }

    private void Unpin_Click(object sender, RoutedEventArgs e)
    {
        ShouldUnpin = true;
        DialogResult = true;
    }
}
