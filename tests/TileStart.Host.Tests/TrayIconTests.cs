using System.Reflection;
using Forms = System.Windows.Forms;
using TileStart.Host;
using TileStart.Host.Themes;

namespace TileStart.Host.Tests;

public sealed class TrayIconTests
{
    [Fact]
    public void MenuItemsInvokeExpectedCallbacksAndTogglePauseState()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var showCount = 0;
                var nativeCount = 0;
                var settingsCount = 0;
                var exitCount = 0;
                var pauseStates = new List<bool>();
                using var tray = new TrayIcon(
                    () => showCount++,
                    paused => pauseStates.Add(paused),
                    () => nativeCount++,
                    AppThemeStyle.Windows11,
                    () => settingsCount++,
                    () => exitCount++);

                var notifyIcon = Assert.IsType<Forms.NotifyIcon>(
                    typeof(TrayIcon).GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .GetValue(tray));
                var menu = Assert.IsType<Forms.ContextMenuStrip>(notifyIcon.ContextMenuStrip);
                var renderer = Assert.IsType<TileStartTrayRenderer>(menu.Renderer);
                var accent = Win10Theme.AccentColor;
                Assert.Equal(
                    System.Drawing.Color.FromArgb(accent.A, accent.R, accent.G, accent.B),
                    renderer.ColorTable.MenuItemSelected);

                Find(menu, "打开 TileStart").PerformClick();
                Find(menu, "打开原生开始菜单").PerformClick();
                var pause = Find(menu, "暂停接管");
                pause.PerformClick();
                Assert.True(pause.Checked);
                pause.PerformClick();
                Assert.False(pause.Checked);
                Find(menu, "TileStart 设置…").PerformClick();
                Find(menu, "退出").PerformClick();

                Assert.Equal(1, showCount);
                Assert.Equal(1, nativeCount);
                Assert.Equal(1, settingsCount);
                Assert.Equal(1, exitCount);
                Assert.Equal([true, false], pauseStates);
                Assert.Equal(
                    ["打开 TileStart", "打开原生开始菜单", "暂停接管", "TileStart 设置…", "退出"],
                    menu.Items.OfType<Forms.ToolStripMenuItem>().Select(item => item.Text ?? string.Empty).ToArray());
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "Tray menu test thread timed out.");
        Assert.Null(failure);
    }

    [Fact]
    public void TrayMenuRoundedRegionKeepsCenterAndClipsOuterCorner()
    {
        using var region = TileStartTrayRenderer.CreateRoundedRegion(new System.Drawing.Size(220, 200));

        Assert.True(region.IsVisible(110, 100));
        Assert.False(region.IsVisible(0, 0));
    }

    private static Forms.ToolStripMenuItem Find(Forms.ContextMenuStrip menu, string text)
    {
        return menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == text);
    }
}