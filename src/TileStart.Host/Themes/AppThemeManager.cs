using System.Windows;
using System.Windows.Media;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Themes;

public static class AppThemeManager
{
    private const string ThemePathPrefix = "/TileStart.Host;component/Themes/";

    public static Uri GetThemeUri(AppThemeStyle style) => new(
        ThemePathPrefix + (style == AppThemeStyle.Windows10 ? "Win10Theme.xaml" : "Win11Theme.xaml"),
        UriKind.Relative);

    public static void Apply(ResourceDictionary resources, AppThemeStyle style)
    {
        var dictionaries = resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.StartsWith(ThemePathPrefix, StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary { Source = GetThemeUri(style) };
        if (existing is null)
        {
            dictionaries.Insert(0, replacement);
        }
        else
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = replacement;
        }

        ApplyTileDefaultBackground(resources);
    }

    /// <summary>
    /// Feeds the theme's tile colour to <see cref="TileItem"/> so tiles the user never recoloured
    /// follow the active theme.
    /// </summary>
    private static void ApplyTileDefaultBackground(ResourceDictionary resources)
    {
        if (resources["TileBackground"] is SolidColorBrush brush)
        {
            TileItem.SetThemeDefaultBackgroundColor(brush.Color.ToString());
        }
    }
}
