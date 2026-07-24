using System.IO;

namespace TileStart.Host.Applications;

public sealed record StartMenuShortcut(string Name, string LaunchTarget, DateTime AddedAt, string FolderPath);

public static class StartMenuFolderBuilder
{
    public static IReadOnlyList<AppEntry> Build(IEnumerable<StartMenuShortcut> shortcuts) =>
        BuildLevel(shortcuts.Select(shortcut => new Candidate(shortcut, SplitPath(shortcut.FolderPath))).ToArray());

    private static IReadOnlyList<AppEntry> BuildLevel(IReadOnlyList<Candidate> candidates)
    {
        var applications = candidates
            .Where(candidate => candidate.Folders.Length == 0)
            .Select(candidate => AppEntry.Application(candidate.Shortcut.Name,
                candidate.Shortcut.LaunchTarget,
                candidate.Shortcut.AddedAt));
        var folders = candidates
            .Where(candidate => candidate.Folders.Length > 0)
            .GroupBy(candidate => candidate.Folders[0], StringComparer.CurrentCultureIgnoreCase)
            .Select(group => AppEntry.Folder(group.Key,
                BuildLevel(group.Select(candidate => candidate with { Folders = candidate.Folders[1..] }).ToArray())));

        return applications
            .Concat(folders)
            .GroupBy(entry => (entry.Name, entry.IsFolder), EntryKeyComparer.Instance)
            .Select(group => group.OrderByDescending(entry => entry.AddedAt).First())
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string[] SplitPath(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

    private sealed record Candidate(StartMenuShortcut Shortcut, string[] Folders);

    private sealed class EntryKeyComparer : IEqualityComparer<(string Name, bool IsFolder)>
    {
        public static EntryKeyComparer Instance { get; } = new();

        public bool Equals((string Name, bool IsFolder) x, (string Name, bool IsFolder) y) =>
            x.IsFolder == y.IsFolder && StringComparer.CurrentCultureIgnoreCase.Equals(x.Name, y.Name);

        public int GetHashCode((string Name, bool IsFolder) value) =>
            HashCode.Combine(StringComparer.CurrentCultureIgnoreCase.GetHashCode(value.Name), value.IsFolder);
    }
}