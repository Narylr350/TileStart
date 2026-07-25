namespace TileStart.Host.Tiles.DragDrop;

public sealed class TileDragHitGeometry
{
    private readonly Dictionary<string, double> _detachmentHeights = [];
    private TileGroupDropZone[] _zones = [];

    public TileDragHitGeometry(IEnumerable<TileGroupDropZone> zones)
    {
        Update(zones);
    }

    public void Update(IEnumerable<TileGroupDropZone> zones)
    {
        _zones = zones
            .Select(zone => zone with { DetachmentHeight = GetDetachmentHeight(zone) })
            .ToArray();
    }

    public TileGroupDropZone? FindTarget(
        double draggedLeft,
        double draggedTop,
        double draggedWidth,
        double draggedHeight) =>
        TileAreaDropResolver.FindTargetForDraggedTile(
            _zones,
            draggedLeft,
            draggedTop,
            draggedWidth,
            draggedHeight);

    public TileNewGroupDropTarget FindNewGroupTarget(
        double draggedLeft,
        double draggedTop,
        double draggedHeight,
        int columnSpan,
        int groupColumns) =>
        TileAreaDropResolver.FindNewGroupTargetForDraggedTile(
            _zones,
            draggedLeft,
            draggedTop,
            draggedHeight,
            columnSpan,
            groupColumns);

    private double GetDetachmentHeight(TileGroupDropZone zone)
    {
        if (_detachmentHeights.TryGetValue(zone.GroupId, out var height))
        {
            return height;
        }

        height = double.IsNaN(zone.DetachmentHeight)
            ? zone.Height
            : zone.DetachmentHeight;
        _detachmentHeights.Add(zone.GroupId, height);
        return height;
    }
}