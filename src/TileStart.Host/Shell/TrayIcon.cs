using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace TileStart.Host.Shell;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private readonly Drawing.Icon? _applicationIcon;
    private readonly Drawing.Font _menuFont;

    public TrayIcon(Action showWindow, Action<bool> setPaused, Action openNativeStart, Action exit)
    {
        _menuFont = new Drawing.Font("Segoe UI", 10, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point);
        var menu = CreateContextMenu(_menuFont);
        menu.Items.Add(CreateMenuItem("打开 TileStart", (_, _) => showWindow()));
        menu.Items.Add(CreateMenuItem("打开原生开始菜单", (_, _) => openNativeStart()));
        menu.Items.Add(CreateSeparator());

        _pauseItem = CreateMenuItem("暂停接管");
        _pauseItem.Click += (_, _) =>
        {
            _pauseItem.Checked = !_pauseItem.Checked;
            setPaused(_pauseItem.Checked);
        };
        menu.Items.Add(_pauseItem);

        var startupItem = CreateMenuItem("登录时启动");
        startupItem.Checked = StartupRegistration.IsEnabled();
        startupItem.Click += (_, _) =>
        {
            var enabled = !startupItem.Checked;
            if (StartupRegistration.SetEnabled(enabled))
            {
                startupItem.Checked = enabled;
            }
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("退出", (_, _) => exit()));

        _applicationIcon = LoadApplicationIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "TileStart",
            Icon = _applicationIcon ?? Drawing.SystemIcons.Application,
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
        _applicationIcon?.Dispose();
        _menuFont.Dispose();
    }

    private static Forms.ContextMenuStrip CreateContextMenu(Drawing.Font font)
    {
        var menu = new Forms.ContextMenuStrip
        {
            BackColor = MenuBackgroundColor,
            ForeColor = MenuForegroundColor,
            Font = font,
            Padding = new Forms.Padding(4),
            ShowCheckMargin = true,
            ShowImageMargin = false,
        };
        if (!Forms.SystemInformation.HighContrast)
        {
            menu.Renderer = new TileStartTrayRenderer();
        }

        return menu;
    }

    private static Forms.ToolStripMenuItem CreateMenuItem(string text, EventHandler? click = null)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            AutoSize = false,
            BackColor = MenuBackgroundColor,
            ForeColor = MenuForegroundColor,
            Padding = new Forms.Padding(8, 0, 14, 0),
            Size = new Drawing.Size(220, 32),
        };
        if (click is not null)
        {
            item.Click += click;
        }

        return item;
    }

    private static Forms.ToolStripSeparator CreateSeparator() =>
        new()
        {
            AutoSize = false,
            BackColor = MenuBackgroundColor,
            Margin = Forms.Padding.Empty,
            Size = new Drawing.Size(220, 7),
        };

    private static Drawing.Color MenuBackgroundColor => Forms.SystemInformation.HighContrast
        ? Drawing.SystemColors.Menu
        : TileStartTrayRenderer.BackgroundColor;

    private static Drawing.Color MenuForegroundColor => Forms.SystemInformation.HighContrast
        ? Drawing.SystemColors.MenuText
        : Drawing.Color.White;

    private static Drawing.Icon? LoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        try
        {
            return Drawing.Icon.ExtractAssociatedIcon(executablePath);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}