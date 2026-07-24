using System.Reflection;
using Forms = System.Windows.Forms;
using TileStart.Host;

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
                var backupCount = 0;
                var exitCount = 0;
                var pauseStates = new List<bool>();
                using var tray = new TrayIcon(
                    () => showCount++,
                    paused => pauseStates.Add(paused),
                    () => nativeCount++,
                    () => backupCount++,
                    () => exitCount++);

                var notifyIcon = Assert.IsType<Forms.NotifyIcon>(
                    typeof(TrayIcon).GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray));
                var menu = Assert.IsType<Forms.ContextMenuStrip>(notifyIcon.ContextMenuStrip);

                Find(menu, "打开 TileStart").PerformClick();
                Find(menu, "打开原生开始菜单").PerformClick();
                var pause = Find(menu, "暂停接管");
                pause.PerformClick();
                Assert.True(pause.Checked);
                pause.PerformClick();
                Assert.False(pause.Checked);
                Find(menu, "备份与恢复…").PerformClick();
                Find(menu, "退出").PerformClick();

                Assert.Equal(1, showCount);
                Assert.Equal(1, nativeCount);
                Assert.Equal(1, backupCount);
                Assert.Equal(1, exitCount);
                Assert.Equal([true, false], pauseStates);
                Assert.NotNull(menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == "登录时启动"));
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

    private static Forms.ToolStripMenuItem Find(Forms.ContextMenuStrip menu, string text)
    {
        return menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == text);
    }
}
