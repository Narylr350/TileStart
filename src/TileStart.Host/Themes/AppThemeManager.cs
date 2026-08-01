using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Themes;

public static class AppThemeManager
{
    private const string ThemePathPrefix = "/TileStart.Host;component/Themes/";

    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static AppThemeStyle CurrentStyle { get; private set; } = AppThemeStyle.Windows10;

    public static Uri GetThemeUri(AppThemeStyle style, bool useDarkMode = true)
    {
        var styleName = style == AppThemeStyle.Windows10 ? "Win10" : "Win11";
        var colorName = useDarkMode ? "Theme" : "LightTheme";
        return new Uri(ThemePathPrefix + styleName + colorName + ".xaml", UriKind.Relative);
    }

    public static bool ResolveDarkMode(AppColorMode colorMode) => colorMode switch
    {
        AppColorMode.Light => false,
        AppColorMode.Dark => true,
        _ => IsSystemDarkMode(),
    };

    internal static bool ResolveDarkMode(AppColorMode colorMode, bool systemUsesLightTheme) => colorMode switch
    {
        AppColorMode.Light => false,
        AppColorMode.Dark => true,
        _ => !systemUsesLightTheme,
    };

    public static bool IsSystemDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
        var systemUsesLightTheme = key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        return ResolveDarkMode(AppColorMode.System, systemUsesLightTheme);
    }

    public static void Apply(ResourceDictionary resources, AppThemeStyle style, AppColorMode colorMode)
    {
        CurrentStyle = style;
        var dictionaries = resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary => IsThemeDictionary(dictionary.Source));
        var replacement = new ResourceDictionary { Source = GetThemeUri(style, ResolveDarkMode(colorMode)) };
        if (existing is null)
        {
            dictionaries.Insert(0, replacement);
        }
        else
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = replacement;
        }

        ApplyDefaultTileBackground(resources, style);
    }

    private static bool IsThemeDictionary(Uri? source)
    {
        var path = source?.OriginalString;
        return path is not null
               && (path.Equals(ThemePathPrefix + "Win10Theme.xaml", StringComparison.OrdinalIgnoreCase)
                   || path.Equals(ThemePathPrefix + "Win11Theme.xaml", StringComparison.OrdinalIgnoreCase)
                   || path.Equals(ThemePathPrefix + "Win10LightTheme.xaml", StringComparison.OrdinalIgnoreCase)
                   || path.Equals(ThemePathPrefix + "Win11LightTheme.xaml", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Feeds the theme's tile colour to <see cref="TileItem"/> so tiles the user never recoloured
    /// follow the active theme.
    /// </summary>
    private static void ApplyDefaultTileBackground(ResourceDictionary resources, AppThemeStyle style)
    {
        if (resources["TileStartDefaultTileBackgroundBrush"] is SolidColorBrush brush)
        {
            var useAcrylic = style == AppThemeStyle.Windows10
                             && Win10Theme.ReadStartMaterial(style).UseAcrylic;
            TileItem.SetThemeDefaultBackgroundColor(
                brush.Color.ToString(),
                ResolveDefaultTileBackgroundOpacity(style, useAcrylic, brush.Opacity));
        }
    }

    internal static double ResolveDefaultTileBackgroundOpacity(
        AppThemeStyle style,
        bool useAcrylic,
        double transparencyEnabledOpacity) =>
        style == AppThemeStyle.Windows10 && useAcrylic
            ? Math.Clamp(transparencyEnabledOpacity, 0, 1)
            : 1;
}
