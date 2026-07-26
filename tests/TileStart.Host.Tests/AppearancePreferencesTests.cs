using TileStart.Host.Themes;

namespace TileStart.Host.Tests;

public sealed class AppearancePreferencesTests
{
    [Fact]
    public void DefaultsToWindows11Style()
    {
        Assert.Equal(AppThemeStyle.Windows11, new AppearancePreferences().ThemeStyle);
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
    [InlineData(AppThemeStyle.Windows10, "Win10Theme.xaml")]
    [InlineData(AppThemeStyle.Windows11, "Win11Theme.xaml")]
    public void ResolvesThemeDictionary(AppThemeStyle style, string fileName)
    {
        Assert.EndsWith(fileName, AppThemeManager.GetThemeUri(style).OriginalString);
    }
}
