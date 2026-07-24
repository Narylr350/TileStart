namespace TileStart.Host.Tiles.Layout;

public sealed class TileReflowStability
{
    public const double RestartDistance = 3;

    private readonly double _restartDistanceSquared;
    private string? _targetKey;
    private System.Windows.Point _timerAnchor;

    public TileReflowStability(double restartDistance = RestartDistance)
    {
        _restartDistanceSquared = restartDistance * restartDistance;
    }

    public bool Observe(string targetKey, System.Windows.Point pointer)
    {
        if (_targetKey == targetKey && (pointer - _timerAnchor).LengthSquared <= _restartDistanceSquared)
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
