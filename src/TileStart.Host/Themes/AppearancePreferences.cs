namespace TileStart.Host.Themes;

public enum AppThemeStyle
{
    Windows10,
    Windows11,
}

public enum AppColorMode
{
    System,
    Light,
    Dark,
}

public sealed class AppearancePreferences
{
    public AppThemeStyle ThemeStyle { get; set; } = AppThemeStyle.Windows11;
    public AppColorMode ColorMode { get; set; } = AppColorMode.System;
}
