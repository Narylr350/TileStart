using System.Threading;
using System.Windows.Controls;
using TileStart.Host.Controllers;

namespace TileStart.Host.Tests;

public sealed class ContextMenuDismissalTests
{
    [Fact]
    public void WindowDismissalClosesTopLevelAndSubmenuPopups()
    {
        RunOnSta(() =>
        {
            var submenu = new MenuItem { Header = "子菜单" };
            submenu.Items.Add(new MenuItem { Header = "项目" });
            var menu = new ContextMenu();
            menu.Items.Add(submenu);
            menu.IsOpen = true;
            submenu.IsSubmenuOpen = true;
            var hasOpenContextMenu = true;

            TileWorkspaceController.CloseContextMenu(menu, value => hasOpenContextMenu = value);

            Assert.False(menu.IsOpen);
            Assert.False(submenu.IsSubmenuOpen);
            Assert.False(hasOpenContextMenu);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(error);
    }
}
