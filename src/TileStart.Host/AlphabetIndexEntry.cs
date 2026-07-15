using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TileStart.Host;

public sealed class AlphabetIndexEntry : INotifyPropertyChanged
{
    private bool _isAvailable;

    public AlphabetIndexEntry(string label)
    {
        Label = label;
    }

    public string Label { get; }

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
        [new("#"), .. Enumerable.Range('A', 26).Select(value => new AlphabetIndexEntry(((char)value).ToString()))];

    public static void UpdateAvailability(IEnumerable<AlphabetIndexEntry> entries, IEnumerable<AppEntry> apps)
    {
        var available = apps.Select(app => app.SortLetter).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            entry.IsAvailable = available.Contains(entry.Label);
        }
    }
}
