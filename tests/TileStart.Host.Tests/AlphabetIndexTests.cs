using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class AlphabetIndexTests
{
    [Fact]
    public void CreateReturnsWin10SemanticZoomOrder()
    {
        var entries = AlphabetIndex.Create();

        Assert.Equal(30, entries.Count);
        Assert.True(entries[0].IsRecent);
        Assert.True(entries[0].IsGlyph);
        Assert.Equal("&", entries[1].Label);
        Assert.Equal("#", entries[2].TargetLetter);
        Assert.Equal("A", entries[3].TargetLetter);
        Assert.Equal("Z", entries[^2].TargetLetter);
        Assert.True(entries[^1].IsGlyph);
    }

    [Fact]
    public void UpdateAvailabilityEnablesOnlyExistingGroups()
    {
        var entries = AlphabetIndex.Create();
        var apps = new[]
        {
            App("工具", "#"),
            App("DataGrip", "D"),
            App("Visual Studio", "V"),
        };

        AlphabetIndex.UpdateAvailability(entries, apps, hasRecentApps: true);

        Assert.True(entries.Single(entry => entry.IsRecent).IsAvailable);
        Assert.False(entries.Single(entry => entry.Label == "&").IsAvailable);
        Assert.True(entries.Single(entry => entry.TargetLetter == "#").IsAvailable);
        Assert.True(entries.Single(entry => entry.TargetLetter == "D").IsAvailable);
        Assert.True(entries.Single(entry => entry.TargetLetter == "V").IsAvailable);
        Assert.False(entries.Single(entry => entry.TargetLetter == "A").IsAvailable);
        Assert.False(entries.Single(entry => entry.TargetLetter == "Z").IsAvailable);
        Assert.False(entries[^1].IsAvailable);
    }

    [Fact]
    public void RecentEntryIsDisabledWhenRecentListIsEmpty()
    {
        var entries = AlphabetIndex.Create();

        AlphabetIndex.UpdateAvailability(entries, [], hasRecentApps: false);

        Assert.False(entries.Single(entry => entry.IsRecent).IsAvailable);
    }

    private static AppEntry App(string name, string sortLetter) => new()
    {
        Name = name,
        LaunchTarget = name,
        SortLetter = sortLetter,
        Initial = name[0].ToString(),
        AddedAt = DateTime.MinValue,
    };
}
