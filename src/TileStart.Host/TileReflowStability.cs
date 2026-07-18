namespace TileStart.Host;

public sealed class TileReflowStability
{
    public const double RestartDistance = 3;
    private const double RestartDistanceSquared = RestartDistance * RestartDistance;

    private string? _targetKey;
    private System.Windows.Point _timerAnchor;

    public bool Observe(string targetKey, System.Windows.Point pointer)
    {
        if (_targetKey == targetKey && (pointer - _timerAnchor).LengthSquared <= RestartDistanceSquared)
        {
            return false;
        }

        _targetKey = targetKey;
        _timerAnchor = pointer;
        return true;
    }

    public void Reset()
    {
        _targetKey = null;
        _timerAnchor = default;
    }
}
