namespace TileStart.Host;

public sealed class TileGroupDragTransaction
{
    private readonly TileLayout _layout;
    private readonly TileGroup _group;
    private readonly TileGroup[] _originalOrder;
    private bool _finished;

    public TileGroupDragTransaction(TileLayout layout, TileGroup group)
    {
        if (!layout.Groups.Contains(group))
        {
            throw new ArgumentException("The dragged group must belong to the layout.", nameof(group));
        }

        _layout = layout;
        _group = group;
        _originalOrder = layout.Groups.ToArray();
    }

    public bool HasChanged => !_layout.Groups.SequenceEqual(_originalOrder);

    public bool Preview(int targetIndex)
    {
        EnsureActive();
        var currentIndex = _layout.Groups.IndexOf(_group);
        targetIndex = Math.Clamp(targetIndex, 0, _layout.Groups.Count - 1);
        if (targetIndex == currentIndex)
        {
            return false;
        }

        _layout.Groups.Move(currentIndex, targetIndex);
        return true;
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
        for (var index = 0; index < _originalOrder.Length; index++)
        {
            var currentIndex = _layout.Groups.IndexOf(_originalOrder[index]);
            if (currentIndex != index)
            {
                _layout.Groups.Move(currentIndex, index);
            }
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