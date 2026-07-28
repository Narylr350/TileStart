using System.Collections.ObjectModel;
using TileStart.Host.Applications;

namespace TileStart.Host.Tiles.Models;

public sealed class TileLayout
{
    public int Version { get; set; }

    public ObservableCollection<TileGroup> Groups { get; set; } = [];

    public IEnumerable<TileItem> EnumerateTiles()
    {
        foreach (var group in Groups)
        {
            foreach (var tile in group.Tiles)
            {
                yield return tile;
                foreach (var child in EnumerateChildren(tile))
                {
                    yield return child;
                }
            }
        }
    }

    public bool ContainsLaunchTarget(string launchTarget, TileItem? excludedTile = null)
    {
        if (string.IsNullOrWhiteSpace(launchTarget))
        {
            return false;
        }

        var identity = LaunchTargetIdentity.GetKey(launchTarget);
        return EnumerateTiles().Any(tile => tile != excludedTile
                                            && !string.IsNullOrWhiteSpace(tile.LaunchTarget)
                                            && LaunchTargetIdentity.GetKey(tile.LaunchTarget) == identity);
    }

    public static string GetIdentityKey(TileItem tile) =>
        string.IsNullOrWhiteSpace(tile.LaunchTarget)
            ? $"ID:{tile.Id}"
            : $"TARGET:{LaunchTargetIdentity.GetKey(tile.LaunchTarget)}";

    private static IEnumerable<TileItem> EnumerateChildren(TileItem tile)
    {
        if (!tile.IsTileFolder)
        {
            yield break;
        }

        foreach (var child in tile.FolderTiles)
        {
            yield return child;
            foreach (var nested in EnumerateChildren(child))
            {
                yield return nested;
            }
        }
    }
}