using TileStart.Host.Applications;
using TileStart.Host.Tiles.Layout;

namespace TileStart.Host.Tiles.Models;

public static class DefaultTileLayout
{
    private sealed record Template(TileSize Size, params string[] Names);

    private static readonly Template[][] GroupTemplates =
    [
        [
            new(TileSize.Medium, "微信", "WeChat"),
            new(TileSize.Medium, "QQ"),
            new(TileSize.Medium, "企业微信", "WeCom"),
            new(TileSize.Medium, "QQ音乐", "QQMusic"),
            new(TileSize.Medium, "MobaXterm"),
            new(TileSize.Medium, "Visual Studio Code"),
            new(TileSize.Medium, "DataGrip"),
            new(TileSize.Medium, "Outlook"),
            new(TileSize.Medium, "IntelliJ IDEA"),
            new(TileSize.Medium, "WebStorm"),
            new(TileSize.Medium, "PyCharm"),
            new(TileSize.Medium, "计算器", "Calculator"),
        ],
        [
            new(TileSize.Medium, "完美世界竞技平台"),
            new(TileSize.Medium, "5E对战平台"),
            new(TileSize.Medium, "Steam"),
            new(TileSize.Large, "Epic Games Launcher"),
            new(TileSize.Medium, "WeGame"),
            new(TileSize.Medium, "MuMu模拟器", "MuMu Player"),
            new(TileSize.Medium, "战网", "Battle.net"),
        ],
        [
            new(TileSize.Wide, "Microsoft Store"),
            new(TileSize.Medium, "Armoury Crate"),
            new(TileSize.Medium, "NVIDIA App"),
            new(TileSize.Medium, "剪映专业版", "CapCut"),
            new(TileSize.Medium, "Microsoft Edge"),
            new(TileSize.Medium, "Everything"),
            new(TileSize.Medium, "Clash Party"),
        ],
    ];

    public static TileLayout Create(IReadOnlyList<AppEntry> apps)
    {
        var layout = new TileLayout();
        var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var templates in GroupTemplates)
        {
            var group = new TileGroup();
            foreach (var template in templates)
            {
                var app = FindApp(apps, template.Names);
                if (app is null || !usedTargets.Add(app.LaunchTarget))
                {
                    continue;
                }

                group.Tiles.Add(new TileItem
                {
                    Name = app.Name,
                    LaunchTarget = app.LaunchTarget,
                    Size = template.Size,
                    Icon = app.Icon,
                });
            }

            if (group.Tiles.Count > 0)
            {
                Win10GroupLayout.Normalize(group);
                layout.Groups.Add(group);
            }
        }

        if (layout.Groups.Count == 0)
        {
            var group = new TileGroup();
            foreach (var app in apps.Take(12))
            {
                group.Tiles.Add(new TileItem
                {
                    Name = app.Name,
                    LaunchTarget = app.LaunchTarget,
                    Size = TileSize.Medium,
                    Icon = app.Icon,
                });
            }

            Win10GroupLayout.Normalize(group);
            layout.Groups.Add(group);
        }

        return layout;
    }

    private static AppEntry? FindApp(IEnumerable<AppEntry> apps, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var exact = apps.FirstOrDefault(app => app.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        foreach (var name in names.Where(name => name.Length > 2))
        {
            var partial =
                apps.FirstOrDefault(app => app.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));
            if (partial is not null)
            {
                return partial;
            }
        }

        return null;
    }
}