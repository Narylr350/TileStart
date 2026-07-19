namespace TileStart.Host;

public sealed class TileGroupDragTransaction
{
    private readonly TileLayout _layout;
    private readonly TileGroup _group;
    private readonly int _columns;
    private readonly Dictionary<TileGroup, TileGroupCell> _originalCells;
    private bool _finished;

    public TileGroupDragTransaction(TileLayout layout, TileGroup group, int columns = 0)
    {
        if (!layout.Groups.Contains(group))
        {
            throw new ArgumentException("The dragged group must belong to the layout.", nameof(group));
        }

        _layout = layout;
        _group = group;
        _columns = columns > 0
            ? columns
            : Math.Max(1, layout.Groups.Select(candidate => candidate.GroupColumn + 1).DefaultIfEmpty(1).Max());
        Win10GroupGridLayout.EnsureCoordinates(layout, _columns);
        _originalCells = layout.Groups.ToDictionary(candidate => candidate, Win10GroupGridLayout.GetCell);
    }

    public bool HasChanged => _originalCells.Any(pair => Win10GroupGridLayout.GetCell(pair.Key) != pair.Value);

    public bool Preview(TileGroupCell target)
    {
        EnsureActive();
        return Win10GroupGridLayout.Move(_layout, _group, target, _columns);
    }

    public bool Commit()
    {
        EnsureActive();
        _finished = true;
        return HasChanged;
    }

    public bool Cancel()
    {
        EnsureActive();
        var changed = HasChanged;
        foreach (var (group, cell) in _originalCells)
        {
            Win10GroupGridLayout.SetCell(group, cell);
        }

        _finished = true;
        return changed;
    }

    private void EnsureActive()
    {
        if (_finished)
        {
            throw new InvalidOperationException("The group drag transaction has already finished.");
        }
    }
}