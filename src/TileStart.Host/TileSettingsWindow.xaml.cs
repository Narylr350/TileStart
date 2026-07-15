using System.IO;
using System.Windows;
using ColorDialog = System.Windows.Forms.ColorDialog;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace TileStart.Host;

public partial class TileSettingsWindow : Window
{
    public TileSettingsWindow(TileItem tile, bool isNew = false)
    {
        InitializeComponent();
        NameBox.Text = tile.Name;
        SubtitleBox.Text = tile.Subtitle;
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
        TargetPanel.Visibility = isNew || tile.TargetType == TileTargetType.Command ? Visibility.Visible : Visibility.Collapsed;
        UnpinButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
    }

    public string TileName => NameBox.Text.Trim();
    public string Subtitle => SubtitleBox.Text.Trim();
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

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            InitialDirectory = Directory.Exists(WorkingDirectory) ? WorkingDirectory : string.Empty,
        };
        if (dialog.ShowDialog() == FormsDialogResult.OK)
        {
            WorkingDirectoryBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "图标来源|*.ico;*.exe;*.dll;*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
            FileName = IconPath,
        };
        if (dialog.ShowDialog(this) == true)
        {
            IconPathBox.Text = dialog.FileName;
        }
    }

    private void BrowseBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
            FileName = BackgroundImagePath,
        };
        if (dialog.ShowDialog(this) == true)
        {
            BackgroundImagePathBox.Text = dialog.FileName;
        }
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TileName))
        {
            MessageBox.Show(this, "磁贴名称不能为空。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (TargetPanel.Visibility == Visibility.Visible && string.IsNullOrWhiteSpace(LaunchTarget))
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
            return System.Windows.Media.ColorConverter.ConvertFromString(value) is not null;
        }
        catch (FormatException)
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
