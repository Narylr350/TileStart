using System.ComponentModel;

namespace TileStart.Host.Applications;

public sealed class AlphabetIndexEntry : INotifyPropertyChanged
{
    private bool _isAvailable;

    public AlphabetIndexEntry(string label, string? targetLetter = null, bool isGlyph = false, bool isRecent = false)
    {
        Label = label;
        TargetLetter = targetLetter;
        IsGlyph = isGlyph;
        IsRecent = isRecent;
    }

    public string Label { get; }

    public string? TargetLetter { get; }

    public bool IsGlyph { get; }

    public bool IsRecent { get; }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable == value)
            {
                return;
            }

            _isAvailable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAvailable)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class AlphabetIndex
{
    public static IReadOnlyList<AlphabetIndexEntry> Create() =>
    [
        new("\uE121", isGlyph: true, isRecent: true),
        new("&"),
        new("#", "#"),
        .. Enumerable.Range('A', 26).Select(value =>
        {
            var letter = ((char)value).ToString();
            return new AlphabetIndexEntry(letter, letter);
        }),
        new("\uE128", isGlyph: true),
    ];

    public static void UpdateAvailability(IEnumerable<AlphabetIndexEntry> entries, IEnumerable<AppEntry> apps,
        bool hasRecentApps)
    {
        var available = apps.Select(app => app.SortLetter).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            entry.IsAvailable = entry.IsRecent
                ? hasRecentApps
                : entry.TargetLetter is not null && available.Contains(entry.TargetLetter);
        }
    }
}