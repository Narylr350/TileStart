using Microsoft.Win32;
using TileStart.Host.Themes;

namespace TileStart.Host.Tests;

public sealed class AppearancePreferencesTests
{
    [Fact]
    public void DefaultsToWindows11Style()
    {
        var preferences = new AppearancePreferences();

        Assert.Equal(AppThemeStyle.Windows11, preferences.ThemeStyle);
        Assert.Equal(AppColorMode.System, preferences.ColorMode);
    }

    [Theory]
    [InlineData(AppThemeStyle.Windows10, "Windows10")]
    [InlineData(AppThemeStyle.Windows11, "Windows11")]
    public void RoundTripsThemeStyle(AppThemeStyle style, string serializedName)
    {
        var json = AppearancePreferencesStore.Serialize(new AppearancePreferences { ThemeStyle = style });
        var restored = AppearancePreferencesStore.Deserialize(json);

        Assert.Contains(serializedName, json);
        Assert.Equal(style, restored.ThemeStyle);
    }

    [Theory]
    [InlineData(AppColorMode.System, "System")]
    [InlineData(AppColorMode.Light, "Light")]
    [InlineData(AppColorMode.Dark, "Dark")]
    public void RoundTripsColorMode(AppColorMode colorMode, string serializedName)
    {
        var json = AppearancePreferencesStore.Serialize(new AppearancePreferences { ColorMode = colorMode });
        var restored = AppearancePreferencesStore.Deserialize(json);

        Assert.Contains(serializedName, json);
        Assert.Equal(colorMode, restored.ColorMode);
    }

    [Theory]
    [InlineData(AppThemeStyle.Windows10, "Win10Theme.xaml")]
    [InlineData(AppThemeStyle.Windows11, "Win11Theme.xaml")]
    public void ResolvesThemeDictionary(AppThemeStyle style, string fileName)
    {
        Assert.EndsWith(fileName, AppThemeManager.GetThemeUri(style).OriginalString);
    }

    [Theory]
    [InlineData(AppThemeStyle.Windows10, "Win10LightTheme.xaml")]
    [InlineData(AppThemeStyle.Windows11, "Win11LightTheme.xaml")]
    public void ResolvesLightThemeDictionary(AppThemeStyle style, string fileName)
    {
        Assert.EndsWith(fileName, AppThemeManager.GetThemeUri(style, useDarkMode: false).OriginalString);
    }

    [Theory]
    [InlineData(AppColorMode.Light, false, false)]
    [InlineData(AppColorMode.Light, true, false)]
    [InlineData(AppColorMode.Dark, false, true)]
    [InlineData(AppColorMode.Dark, true, true)]
    [InlineData(AppColorMode.System, false, true)]
    [InlineData(AppColorMode.System, true, false)]
    public void ResolvesExplicitAndSystemColorModes(
        AppColorMode colorMode,
        bool systemUsesLightTheme,
        bool expectedDarkMode)
    {
        Assert.Equal(expectedDarkMode, AppThemeManager.ResolveDarkMode(colorMode, systemUsesLightTheme));
    }

    [Theory]
    [InlineData(AppColorMode.System)]
    [InlineData(AppColorMode.Light)]
    [InlineData(AppColorMode.Dark)]
    public void SystemAccentChangesRestartEveryColorMode(AppColorMode colorMode)
    {
        Assert.True(App.ShouldRestartForUserPreferenceChange(
            UserPreferenceCategory.Color,
            colorMode,
            previousDarkMode: true,
            resolvedDarkMode: true));
    }

    [Fact]
    public void SystemColorModeRestartsWhenDarkModeChanges()
    {
        Assert.True(App.ShouldRestartForUserPreferenceChange(
            UserPreferenceCategory.General,
            AppColorMode.System,
            previousDarkMode: true,
            resolvedDarkMode: false));
    }

    [Fact]
    public void UnrelatedPreferenceChangesDoNotRestartAnExplicitColorMode()
    {
        Assert.False(App.ShouldRestartForUserPreferenceChange(
            UserPreferenceCategory.General,
            AppColorMode.Dark,
            previousDarkMode: true,
            resolvedDarkMode: true));
    }
}
