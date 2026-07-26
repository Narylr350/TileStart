using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using TileStart.Host.Themes;

namespace TileStart.Host.Shell;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private readonly Drawing.Icon? _applicationIcon;
    private readonly Drawing.Font _menuFont;
    private readonly TileStartTrayPalette _palette;

    public TrayIcon(Action showWindow, Action<bool> setPaused, Action openNativeStart, Func<Task> checkForUpdates,
        Action openBackupAndRestore, Action openAbout, AppThemeStyle themeStyle,
        Action<AppThemeStyle> setThemeStyle, Action exit)
    {
        _palette = TileStartTrayRenderer.GetPalette(themeStyle);
        _menuFont = new Drawing.Font(
            themeStyle == AppThemeStyle.Windows11 ? "Segoe UI Variable Text" : "Segoe UI",
            10,
            Drawing.FontStyle.Regular,
            Drawing.GraphicsUnit.Point);
        var menu = CreateContextMenu(_menuFont, themeStyle);
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

        var appearanceItem = CreateMenuItem("界面风格");
        var windows10Item = CreateMenuItem("Windows 10");
        var windows11Item = CreateMenuItem("Windows 11");
        windows10Item.Checked = themeStyle == AppThemeStyle.Windows10;
        windows11Item.Checked = themeStyle == AppThemeStyle.Windows11;
        windows10Item.Click += (_, _) => setThemeStyle(AppThemeStyle.Windows10);
        windows11Item.Click += (_, _) => setThemeStyle(AppThemeStyle.Windows11);
        appearanceItem.DropDownItems.Add(windows10Item);
        appearanceItem.DropDownItems.Add(windows11Item);
        ConfigureDropDown(appearanceItem.DropDown, themeStyle);
        menu.Items.Add(appearanceItem);

        menu.Items.Add(CreateMenuItem("检查更新…", async (_, _) => await checkForUpdates()));
        menu.Items.Add(CreateMenuItem("备份与恢复…", (_, _) => openBackupAndRestore()));
        menu.Items.Add(CreateMenuItem("关于 TileStart", (_, _) => openAbout()));
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

    private Forms.ContextMenuStrip CreateContextMenu(Drawing.Font font, AppThemeStyle themeStyle)
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
        ConfigureDropDown(menu, themeStyle);
        return menu;
    }

    private void ConfigureDropDown(Forms.ToolStripDropDown menu, AppThemeStyle themeStyle)
    {
        menu.BackColor = MenuBackgroundColor;
        menu.ForeColor = MenuForegroundColor;
        menu.Font = _menuFont;
        menu.Padding = new Forms.Padding(4);
        if (Forms.SystemInformation.HighContrast)
        {
            return;
        }

        menu.Renderer = new TileStartTrayRenderer(themeStyle);
        if (themeStyle == AppThemeStyle.Windows11)
        {
            menu.Opened += (_, _) => ApplyRoundedRegion(menu);
            menu.SizeChanged += (_, _) => ApplyRoundedRegion(menu);
        }
    }

    private static void ApplyRoundedRegion(Forms.ToolStripDropDown menu)
    {
        if (menu.Width <= 0 || menu.Height <= 0)
        {
            return;
        }

        var previous = menu.Region;
        menu.Region = TileStartTrayRenderer.CreateRoundedRegion(menu.Size);
        previous?.Dispose();
    }

    private Forms.ToolStripMenuItem CreateMenuItem(string text, EventHandler? click = null)
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

    private Forms.ToolStripSeparator CreateSeparator() =>
        new()
        {
            AutoSize = false,
            BackColor = MenuBackgroundColor,
            Margin = Forms.Padding.Empty,
            Size = new Drawing.Size(220, 7),
        };

    private Drawing.Color MenuBackgroundColor => Forms.SystemInformation.HighContrast
        ? Drawing.SystemColors.Menu
        : _palette.Background;

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
