using System.Windows;

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
            return;
        }

        var index = dictionaries.IndexOf(existing);
        dictionaries[index] = replacement;
    }
}
