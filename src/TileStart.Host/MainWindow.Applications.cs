using System.Windows;
using Button = System.Windows.Controls.Button;
using TileStart.Host.Applications;
using TileStart.Host.Shell;

namespace TileStart.Host;

public partial class MainWindow
{
    public void HandleHostRequest(HostRequest request) =>
        _appController.HandleHostRequest(request);

    private void RecentExpandButton_Click(object sender, RoutedEventArgs e) =>
        _appController.RecentExpandButtonClick();

    private void AppButton_Click(object sender, RoutedEventArgs e)
    {
        if (Environment.TickCount64 < _suppressTileActivationUntil)
        {
            return;
        }

        if (sender is not Button { Tag: AppEntry app })
        {
            return;
        }

        _ = _appController.AppButtonClick(app);
    }
}