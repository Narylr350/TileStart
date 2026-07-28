using System.Threading;
using System.Windows.Controls;
using TileStart.Host.Controllers;

namespace TileStart.Host.Tests;

public sealed class ContextMenuDismissalTests
{
    [Fact]
    public void WindowDismissalClearsTrackedStateWithoutOpeningANativePopup()
    {
        RunOnSta(() =>
        {
            var menu = new ContextMenu();
            var hasOpenContextMenu = true;

            TileWorkspaceController.CloseContextMenu(menu, value => hasOpenContextMenu = value);

            Assert.False(menu.IsOpen);
            Assert.False(hasOpenContextMenu);
        });
    }

    [Fact]
    public void MenuEnumerationIncludesNestedItems()
    {
        RunOnSta(() =>
        {
            var child = new MenuItem { Header = "项目" };
            var submenu = new MenuItem { Header = "子菜单" };
            submenu.Items.Add(child);
            var menu = new ContextMenu();
            menu.Items.Add(submenu);

            Assert.Equal([submenu, child], TileWorkspaceController.EnumerateMenuItems(menu));
        });
    }

    [Fact]
    public void ClosingTheTopLevelMenuAlsoClearsNestedSubmenuState()
    {
        RunOnSta(() =>
        {
            var nested = new MenuItem { Header = "二级" };
            nested.Items.Add(new MenuItem { Header = "操作" });
            nested.IsSubmenuOpen = true;
            var menu = new ContextMenu();
            menu.Items.Add(nested);

            TileWorkspaceController.CloseSubmenus(menu);

            Assert.False(nested.IsSubmenuOpen);
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
