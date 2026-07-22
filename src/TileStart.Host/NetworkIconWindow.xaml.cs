using System.Windows;
using System.Windows.Input;

namespace TileStart.Host;

public partial class NetworkIconWindow : Window
{
    public NetworkIconWindow(string sourceUrl = "")
    {
        InitializeComponent();
        UrlBox.Text = sourceUrl;
    }

    public string IconPath { get; private set; } = string.Empty;
    public string SourceUrl => UrlBox.Text.Trim();

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        DownloadButton.IsEnabled = false;
        ConfirmButton.IsEnabled = false;
        StatusText.Text = "正在下载图标…";
        try
        {
            IconPath = await CustomIconStore.DownloadAsync(SourceUrl);
            PreviewImage.Source = ShellIconLoader.LoadImage(IconPath);
            ConfirmButton.IsEnabled = PreviewImage.Source is not null;
            StatusText.Text = ConfirmButton.IsEnabled ? "图标已缓存，可以使用。" : "图标无法预览。";
        }
        catch (Exception exception)
        {
            IconPath = string.Empty;
            PreviewImage.Source = null;
            StatusText.Text = exception.Message;
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(IconPath))
        {
            DialogResult = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
