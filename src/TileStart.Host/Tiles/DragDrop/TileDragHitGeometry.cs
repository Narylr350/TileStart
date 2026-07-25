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
        var current = zones.ToArray();
        var rowHeights = current
            .Where(zone => zone.GroupRow >= 0)
            .GroupBy(zone => zone.GroupRow)
            .ToDictionary(
                row => row.Key,
                row => row.Max(EffectiveDetachmentHeight));
        _zones = current
            .Select(zone => zone with
            {
                DetachmentHeight = GetDetachmentHeight(
                    zone,
                    rowHeights.GetValueOrDefault(zone.GroupRow, EffectiveDetachmentHeight(zone))),
            })
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

    private double GetDetachmentHeight(TileGroupDropZone zone, double rowHeight)
    {
        if (_detachmentHeights.TryGetValue(zone.GroupId, out var height))
        {
            return height;
        }

        height = Math.Max(EffectiveDetachmentHeight(zone), rowHeight);
        _detachmentHeights.Add(zone.GroupId, height);
        return height;
    }

    private static double EffectiveDetachmentHeight(TileGroupDropZone zone) =>
        double.IsNaN(zone.DetachmentHeight)
            ? zone.Height
            : zone.DetachmentHeight;
}