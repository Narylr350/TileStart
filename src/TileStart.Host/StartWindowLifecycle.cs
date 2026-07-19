namespace TileStart.Host;

public static class StartWindowLifecycle
{
    public static bool HasAcquiredForeground(
        bool alreadyAcquired,
        bool foregroundBelongsToStart,
        bool receivedNativeActivation) =>
        alreadyAcquired || foregroundBelongsToStart || receivedNativeActivation;

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