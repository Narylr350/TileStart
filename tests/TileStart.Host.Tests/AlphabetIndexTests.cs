using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class AlphabetIndexTests
{
    [Fact]
    public void CreateReturnsHashAndAlphabetInWin10Order()
    {
        var entries = AlphabetIndex.Create();

        Assert.Equal(27, entries.Count);
        Assert.Equal("#", entries[0].Label);
        Assert.Equal("A", entries[1].Label);
        Assert.Equal("Z", entries[^1].Label);
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

        AlphabetIndex.UpdateAvailability(entries, apps);

        Assert.True(entries.Single(entry => entry.Label == "#").IsAvailable);
        Assert.True(entries.Single(entry => entry.Label == "D").IsAvailable);
        Assert.True(entries.Single(entry => entry.Label == "V").IsAvailable);
        Assert.False(entries.Single(entry => entry.Label == "A").IsAvailable);
        Assert.False(entries.Single(entry => entry.Label == "Z").IsAvailable);
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
