using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TileStart.Host.Applications;

public interface IApplicationListItem
{
    string SortLetter { get; }
    string SortName { get; }
}

public sealed class RecentApplicationsSection : IApplicationListItem, INotifyPropertyChanged
{
    public const string GroupKey = "\u0001";

    private bool _isExpanded;
    private bool _canExpand;

    public ObservableCollection<AppEntry> Apps { get; } = [];

    public string SortLetter => GroupKey;
    public string SortName => string.Empty;
    public string ExpandText => _isExpanded ? "折叠" : "展开";
    public string ExpandGlyph => _isExpanded ? "\uE70E" : "\uE70D";
    public bool CanExpand => _canExpand;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Update(bool isExpanded, bool canExpand)
    {
        if (_isExpanded != isExpanded)
        {
            _isExpanded = isExpanded;
            OnPropertyChanged(nameof(ExpandText));
            OnPropertyChanged(nameof(ExpandGlyph));
        }

        if (_canExpand != canExpand)
        {
            _canExpand = canExpand;
            OnPropertyChanged(nameof(CanExpand));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
