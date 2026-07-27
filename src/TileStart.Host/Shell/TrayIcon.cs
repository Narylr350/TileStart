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

    public TrayIcon(Action showWindow, Action<bool> setPaused, Action openNativeStart, AppThemeStyle themeStyle,
        bool useDarkMode,
        Action openSettings, Action exit)
    {
        _palette = TileStartTrayRenderer.GetPalette(themeStyle, useDarkMode);
        _menuFont = new Drawing.Font(
            themeStyle == AppThemeStyle.Windows11 ? "Segoe UI Variable Text" : "Segoe UI",
            10,
            Drawing.FontStyle.Regular,
            Drawing.GraphicsUnit.Point);
        var menu = CreateContextMenu(_menuFont, themeStyle, useDarkMode);
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

        menu.Items.Add(CreateMenuItem("TileStart 设置…", (_, _) => openSettings()));
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

    private Forms.ContextMenuStrip CreateContextMenu(Drawing.Font font, AppThemeStyle themeStyle, bool useDarkMode)
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
        ConfigureDropDown(menu, themeStyle, useDarkMode);
        return menu;
    }

    private void ConfigureDropDown(Forms.ToolStripDropDown menu, AppThemeStyle themeStyle, bool useDarkMode)
    {
        menu.BackColor = MenuBackgroundColor;
        menu.ForeColor = MenuForegroundColor;
        menu.Font = _menuFont;
        menu.Padding = new Forms.Padding(4);
        if (Forms.SystemInformation.HighContrast)
        {
            return;
        }

        menu.Renderer = new TileStartTrayRenderer(themeStyle, useDarkMode);
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

    private Drawing.Color MenuForegroundColor => Forms.SystemInformation.HighContrast
        ? Drawing.SystemColors.MenuText
        : _palette.Text;

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