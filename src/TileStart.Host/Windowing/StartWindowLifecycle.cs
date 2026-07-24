namespace TileStart.Host.Windowing;

public sealed class StartWindowLifecycle
{
    public const int RequiredForeignForegroundObservations = 3;

    private int _foreignForegroundObservations;

    public bool HasAcquiredForeground { get; private set; }

    public int ForeignForegroundObservations => _foreignForegroundObservations;

    public void Reset()
    {
        HasAcquiredForeground = false;
        _foreignForegroundObservations = 0;
    }

    public void ObserveNativeActivation()
    {
        HasAcquiredForeground = true;
        _foreignForegroundObservations = 0;
    }

    public bool ObserveForeground(
        bool foregroundKnown,
        bool foregroundBelongsToStart,
        bool hasActiveOwnedWindow,
        bool hasOpenContextMenu)
    {
        if (foregroundBelongsToStart)
        {
            HasAcquiredForeground = true;
            _foreignForegroundObservations = 0;
            return false;
        }

        if (!HasAcquiredForeground
            || !foregroundKnown
            || hasActiveOwnedWindow
            || hasOpenContextMenu)
        {
            _foreignForegroundObservations = 0;
            return false;
        }

        _foreignForegroundObservations++;
        return _foreignForegroundObservations >= RequiredForeignForegroundObservations;
    }
}