using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using TileStart.Host.Updates;
using MessageBox = System.Windows.MessageBox;

namespace TileStart.Host.About;

public partial class AboutWindow : Window
{
    internal static readonly Uri ProjectUri = new("https://github.com/Narylr350/TileStart");

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"版本 {GitHubUpdateService.CurrentVersion.ToString(3)}";
        EditionText.Text = GitHubUpdateService.IsInstalledCopy(Environment.ProcessPath) ? "安装版" : "便携版";
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProjectUri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, $"无法打开项目页面：{exception.Message}", "TileStart",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
