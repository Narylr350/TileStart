using System.Windows;
using System.Windows.Input;
using WpfAnimatedGif;

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
        SetPreview(string.Empty);
        StatusText.Text = "正在下载图标…";
        try
        {
            IconPath = await CustomIconStore.DownloadAsync(SourceUrl);
            ConfirmButton.IsEnabled = SetPreview(IconPath);
            StatusText.Text = ConfirmButton.IsEnabled ? "图标已缓存，可以使用。" : "图标无法预览。";
        }
        catch (Exception exception)
        {
            IconPath = string.Empty;
            SetPreview(string.Empty);
            StatusText.Text = exception.Message;
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }
    }

    private bool SetPreview(string path)
    {
        ImageBehavior.SetAnimatedSource(PreviewImage, null);
        PreviewImage.Source = null;

        if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            var animatedSource = GifImageSourceConverter.Load(path);
            ImageBehavior.SetAnimatedSource(PreviewImage, animatedSource);
            return animatedSource is not null;
        }

        PreviewImage.Source = ShellIconLoader.LoadImage(path);
        return PreviewImage.Source is not null;
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
