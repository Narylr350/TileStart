using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void MenuItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not MenuItem item
            || item.Template.FindName("MenuItemRoot", item) is not Border root
            || item.TryFindResource("TileStartContextMenuPressedBrush") is not System.Windows.Media.Brush pressedBrush)
        {
            return;
        }

        // WPF MenuItem 没有可供模板 Trigger 使用的公开 IsPressed 状态。
        // 按下期间用本地值覆盖背景，清除后会重新落回 Hover/SubmenuOpened Trigger。
        root.Background = pressedBrush;
    }

    private void MenuItem_ClearPressedState(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item
            && item.Template.FindName("MenuItemRoot", item) is Border root)
        {
            root.ClearValue(Border.BackgroundProperty);
        }
    }
}
