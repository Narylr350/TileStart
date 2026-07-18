namespace TileStart.Host;

public static class StartWindowLifecycle
{
    public static bool HasAcquiredForeground(
        bool alreadyAcquired,
        bool setForegroundSucceeded,
        bool foregroundBelongsToStart) =>
        alreadyAcquired || setForegroundSucceeded || foregroundBelongsToStart;

    public static bool ShouldHideForForegroundChange(
        bool hasAcquiredForeground,
        bool foregroundKnown,
        bool foregroundBelongsToStart,
        bool hasActiveOwnedWindow,
        bool hasOpenContextMenu) =>
        hasAcquiredForeground
        && foregroundKnown
        && !foregroundBelongsToStart
        && !hasActiveOwnedWindow
        && !hasOpenContextMenu;
}
