using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TileStart.Host.Icons;
using TileStart.Host.Shell;

namespace TileStart.Host.Applications;

public sealed class AppEntry : INotifyPropertyChanged, IApplicationListItem
{
    private bool _isExpanded;
    private ImageSource? _icon;

    public required string Name { get; init; }
    public required string LaunchTarget { get; init; }
    public required string SortLetter { get; init; }
    public string SortName => Name;
    public required string Initial { get; init; }
    public required DateTime AddedAt { get; init; }

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
        }
    }

    public string PackageInstallPath { get; init; } = string.Empty;
    public string AppUserModelId { get; init; } = string.Empty;
    public bool IsCustom { get; init; }
    public ObservableCollection<AppEntry> Children { get; init; } = [];

    public bool IsFolder => Children.Count > 0;

    public bool CanUninstall => AppUninstaller.CanUninstall(this);

    public bool CanPinToTaskbar => TaskbarPinner.CanPin(this);

    public bool CanOpenFileLocation
    {
        get
        {
            var extension = Path.GetExtension(LaunchTarget);
            return !IsFolder
                   && ((IsCustom && File.Exists(LaunchTarget))
                       || extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                       || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
                       || extension.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FolderChevron));
        }
    }

    public string FolderChevron => IsExpanded ? "\uE70E" : "\uE76C";

    public event PropertyChangedEventHandler? PropertyChanged;

    public static AppEntry Application(string name, string launchTarget, DateTime addedAt, ImageSource? icon = null,
        string packageInstallPath = "", string appUserModelId = "", bool isCustom = false)
    {
        var (initial, sortLetter) = GetIndex(name);
        return new AppEntry
        {
            Name = name,
            LaunchTarget = launchTarget,
            SortLetter = sortLetter,
            Initial = initial,
            AddedAt = addedAt,
            Icon = icon,
            PackageInstallPath = packageInstallPath,
            AppUserModelId = appUserModelId,
            IsCustom = isCustom,
        };
    }

    public static AppEntry Folder(string name, IEnumerable<AppEntry> children)
    {
        var (initial, sortLetter) = GetIndex(name);
        return new AppEntry
        {
            Name = name,
            LaunchTarget = string.Empty,
            SortLetter = sortLetter,
            Initial = initial,
            AddedAt = DateTime.MinValue,
            Icon = Win10FolderIcon.Image,
            Children = new ObservableCollection<AppEntry>(children),
        };
    }

    public static IEnumerable<AppEntry> FlattenApplications(IEnumerable<AppEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (!entry.IsFolder)
            {
                yield return entry;
                continue;
            }

            foreach (var child in FlattenApplications(entry.Children))
            {
                yield return child;
            }
        }
    }

    private static (string Initial, string SortLetter) GetIndex(string name)
    {
        var first = name.Trim().FirstOrDefault();
        var initial = first == default ? "?" : char.ToUpper(first).ToString();
        var sortLetter = Win10AppGrouping.GetGroupKey(name);
        return (initial, sortLetter);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}