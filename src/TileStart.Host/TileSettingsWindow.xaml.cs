using System.IO;
using System.Windows;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace TileStart.Host;

public partial class TileSettingsWindow : Window
{
    public TileSettingsWindow(TileItem tile)
    {
        InitializeComponent();
        NameBox.Text = tile.Name;
        ArgumentsBox.Text = tile.Arguments;
        WorkingDirectoryBox.Text = tile.WorkingDirectory;
        IconPathBox.Text = tile.IconPath;
        SizeBox.SelectedValue = tile.Size.ToString();
        RunAsAdministratorBox.IsChecked = tile.RunAsAdministrator;
    }

    public string TileName => NameBox.Text.Trim();
    public string Arguments => ArgumentsBox.Text.Trim();
    public string WorkingDirectory => WorkingDirectoryBox.Text.Trim();
    public string IconPath => IconPathBox.Text.Trim();
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TileName))
        {
            MessageBox.Show(this, "磁贴名称不能为空。", "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        DialogResult = true;
    }

    private void Unpin_Click(object sender, RoutedEventArgs e)
    {
        ShouldUnpin = true;
        DialogResult = true;
    }
}
