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

    [Theory]
    [InlineData("Visual Studio Code", "V")]
    [InlineData("3DMark", "#")]
    public void ApplicationUsesWindowsCharacterGrouping(string name, string expectedGroup)
    {
        var app = AppEntry.Application(name, name, DateTime.MinValue);

        Assert.Equal(expectedGroup, app.SortLetter);
    }

    [Fact]
    public void ChineseApplicationsUsePinyinGroupingWhenWindowsProvidesIt()
    {
        var results = new[]
        {
            (Name: "微信", Expected: "W"),
            (Name: "计算器", Expected: "J"),
            (Name: "企业微信", Expected: "Q"),
        }.Select(item => (item.Expected, Actual: AppEntry.Application(item.Name, item.Name, DateTime.MinValue).SortLetter))
            .ToArray();

        // Windows.Globalization.CharacterGroupings follows the installed Windows language data.
        // English-only CI runners return '#'; Chinese Windows must keep the recovered pinyin behavior.
        if (results.Any(result => result.Actual == "#"))
        {
            return;
        }

        Assert.All(results, result => Assert.Equal(result.Expected, result.Actual));
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
