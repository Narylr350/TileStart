using System.Windows;
using TileStart.Host.Navigation;

namespace TileStart.Host.Themes;

public partial class SharedStyles : ResourceDictionary
{
    private void SubmenuPopup_Opened(object? sender, EventArgs e) =>
        MenuPopupAnimator.OpenSubmenu(sender);

    private void SubmenuPopup_Closed(object? sender, EventArgs e) =>
        MenuPopupAnimator.CloseSubmenu(sender);
}
