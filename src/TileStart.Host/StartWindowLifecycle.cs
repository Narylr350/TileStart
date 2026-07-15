namespace TileStart.Host;

public static class StartWindowLifecycle
{
    public static bool ShouldHideAfterDeactivation(bool isActive, bool hasActiveOwnedWindow) =>
        !isActive && !hasActiveOwnedWindow;
}
