namespace TileStart.Host.Themes;

public enum AppThemeStyle
{
    Windows10,
    Windows11,
}

public sealed class AppearancePreferences
{
    public AppThemeStyle ThemeStyle { get; set; } = AppThemeStyle.Windows11;
}
