namespace TileStart.Host.Tiles.DragDrop;

public sealed class TileDragHitGeometry
{
    private readonly TileGroupDropZone[] _zones;

    public TileDragHitGeometry(IEnumerable<TileGroupDropZone> zones)
    {
        _zones = zones
            .Select(zone => zone with
            {
                DetachmentHeight = double.IsNaN(zone.DetachmentHeight)
                    ? zone.Height
                    : zone.DetachmentHeight,
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
}
