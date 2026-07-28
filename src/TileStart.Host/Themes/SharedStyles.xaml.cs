using System.Windows;
using TileStart.Host.Navigation;
using TileStart.Host.Windowing;

namespace TileStart.Host.Themes;

public partial class SharedStyles : ResourceDictionary
{
    private void DialogWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            DialogWindowManager.Attach(window);
        }
    }

    private void SubmenuPopup_Opened(object? sender, EventArgs e) =>
        MenuPopupAnimator.OpenSubmenu(sender);

    private void SubmenuPopup_Closed(object? sender, EventArgs e) =>
        MenuPopupAnimator.CloseSubmenu(sender);
}
