using System.IO;
using System.Windows;
using System.Windows.Controls;
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
    private readonly string _subtitle;

    public TileSettingsWindow(TileItem tile, bool isNew = false)
    {
        InitializeComponent();
        _subtitle = tile.Subtitle;
        NameBox.Text = tile.Name;
        TargetBox.Text = tile.LaunchTarget;
        ArgumentsBox.Text = tile.Arguments;
        WorkingDirectoryBox.Text = tile.WorkingDirectory;
        IconPathBox.Text = tile.IconPath;
        BackgroundImagePathBox.Text = tile.BackgroundImagePath;
        BackgroundColorBox.Text = tile.BackgroundColor;
        ForegroundColorBox.Text = tile.ForegroundColor;
        ShowTitleBox.IsChecked = tile.ShowTitle;
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
    }

    public string TileName => NameBox.Text.Trim();
    public string Subtitle => _subtitle;
    public string LaunchTarget => TargetBox.Text.Trim();
    public string Arguments => ArgumentsBox.Text.Trim();
    public string WorkingDirectory => WorkingDirectoryBox.Text.Trim();
    public string IconPath => IconPathBox.Text.Trim();
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
            Filter = "图标来源|*.ico;*.exe;*.dll;*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
            FileName = File.Exists(IconPath) ? IconPath : string.Empty,
        };
        if (dialog.ShowDialog(this) == true)
        {
            IconPathBox.Text = dialog.FileName;
        }
    }

    private void ClearIcon_Click(object sender, RoutedEventArgs e)
    {
        IconPathBox.Clear();
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
    }

    private void ForegroundColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview(ForegroundColorPreview, ForegroundColorBox.Text);
    }

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
