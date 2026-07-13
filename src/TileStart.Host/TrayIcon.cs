using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace TileStart.Host;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseItem;

    public TrayIcon(Action showWindow, Action<bool> setPaused, Action openNativeStart, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 TileStart", null, (_, _) => showWindow());
        menu.Items.Add("打开原生开始菜单", null, (_, _) => openNativeStart());
        menu.Items.Add(new Forms.ToolStripSeparator());

        _pauseItem = new Forms.ToolStripMenuItem("暂停接管");
        _pauseItem.Click += (_, _) =>
        {
            _pauseItem.Checked = !_pauseItem.Checked;
            setPaused(_pauseItem.Checked);
        };
        menu.Items.Add(_pauseItem);

        var startupItem = new Forms.ToolStripMenuItem("登录时启动")
        {
            Checked = StartupRegistration.IsEnabled(),
        };
        startupItem.Click += (_, _) =>
        {
            var enabled = !startupItem.Checked;
            if (StartupRegistration.SetEnabled(enabled))
            {
                startupItem.Checked = enabled;
            }
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "TileStart",
            Icon = Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => showWindow();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
