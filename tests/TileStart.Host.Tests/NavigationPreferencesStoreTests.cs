using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class NavigationPreferencesStoreTests
{
    [Fact]
    public void DefaultsPreserveTheOriginalCollapsedRailLinks()
    {
        var preferences = new NavigationPreferences();

        Assert.True(preferences.ShowUser);
        Assert.True(preferences.ShowDocuments);
        Assert.False(preferences.ShowDownloads);
        Assert.True(preferences.ShowPictures);
        Assert.False(preferences.ShowFileExplorer);
        Assert.True(preferences.ShowSettings);
        Assert.False(preferences.ShowMusic);
        Assert.False(preferences.ShowVideos);
        Assert.False(preferences.ShowNetwork);
    }

    [Fact]
    public void JsonRoundTripPreservesCustomizedLinks()
    {
        var preferences = new NavigationPreferences
        {
            ShowDownloads = true,
            ShowFileExplorer = true,
            ShowPictures = false,
            ShowNetwork = true,
        };

        var restored = NavigationPreferencesStore.Deserialize(
            NavigationPreferencesStore.Serialize(preferences));

        Assert.True(restored.ShowDownloads);
        Assert.True(restored.ShowFileExplorer);
        Assert.False(restored.ShowPictures);
        Assert.True(restored.ShowNetwork);
    }

    [Fact]
    public void UnknownPreferenceKeysAreIgnored()
    {
        var preferences = new NavigationPreferences();

        preferences.SetVisible("Unknown", true);

        Assert.False(preferences.IsVisible("Unknown"));
        Assert.True(preferences.ShowDocuments);
    }
}
