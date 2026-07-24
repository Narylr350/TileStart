using TileStart.Host;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tiles.DragDrop;

public static class TileDropResolver
{
    public const int ReflowDelayMilliseconds = 120;
    public const int FolderActivationDelayMilliseconds = 320;
    public const double FolderActivationDrift = 12;
    private const double FolderActivationInsetRatio = 0.2;

    public static (int Column, int Row) GetCell(
        System.Windows.Point pointer,
        System.Windows.Point anchor,
        TileItem tile,
        int columns = Win10TileMetrics.GroupColumns)
    {
        var left = pointer.X - anchor.X;
        var top = pointer.Y - anchor.Y;
        return (
            Math.Clamp((int)Math.Round(left / Win10TileMetrics.CellPitch),
                0,
                Math.Max(0, columns - tile.Size.ColumnSpan())),
            Math.Max(0, (int)Math.Round(top / Win10TileMetrics.CellPitch)));
    }

    public static TileItem? FindFolderTarget(
        TileGroup group,
        TileItem moving,
        System.Windows.Point pointer,
        System.Windows.Point anchor,
        TileItem? activeFolderPreview = null)
    {
        if (moving.IsTileFolder)
        {
            return null;
        }

        var left = pointer.X - anchor.X;
        var top = pointer.Y - anchor.Y;
        var centerX = left + moving.PixelWidth / 2;
        var centerY = top + moving.PixelHeight / 2;

        return group.Tiles.FirstOrDefault(tile =>
        {
            if (ReferenceEquals(tile, moving))
            {
                return false;
            }

            var insetX = ReferenceEquals(tile, activeFolderPreview)
                ? 0
                : tile.PixelWidth * FolderActivationInsetRatio;
            var insetY = ReferenceEquals(tile, activeFolderPreview)
                ? 0
                : tile.PixelHeight * FolderActivationInsetRatio;
            return centerX >= tile.Left + insetX
                   && centerX < tile.Left + tile.PixelWidth - insetX
                   && centerY >= tile.DisplayTop + insetY
                   && centerY < tile.DisplayTop + tile.PixelHeight - insetY;
        });
    }

    public static string GetStabilityKey(TileGroup target, int column, int row, TileItem? folderTarget) =>
        folderTarget is not null
            ? $"{target.Id}:folder:{folderTarget.Id}"
            : $"{target.Id}:cell:{column}:{row}";
}