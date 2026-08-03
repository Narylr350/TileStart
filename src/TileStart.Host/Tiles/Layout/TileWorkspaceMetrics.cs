using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tiles.Layout;

public static class TileWorkspaceMetrics
{
    public const int MinimumGroupWidthUnits = 1;
    public const int MaximumGroupWidthUnits = 8;
    public const int MinimumGroupHeightUnits = 1;
    public const int MaximumGroupHeightUnits = 8;
    public const int LegacyGroupWidthUnits = 4;

    // 4 * 99 + 3 * 8 = 420 DIP，保持旧布局组宽不变；107 DIP pitch 则让
    // 两个默认组的 412 DIP 磁贴面在 150% DPI 下相距 24 px，而不是旧实现的 22 px。
    public const double UnitWidth = 99;
    public const double ColumnGap = 8;
    public const double ColumnPitch = UnitWidth + ColumnGap;

    public static int ColumnsForWidth(double availableWidth)
    {
        if (!double.IsFinite(availableWidth))
        {
            return int.MaxValue;
        }

        return Math.Max(1, (int)Math.Floor((availableWidth + ColumnGap + 0.5) / ColumnPitch));
    }

    public static double RequiredWidth(int columns) =>
        columns <= 0 ? 0 : columns * UnitWidth + (columns - 1) * ColumnGap;

    public static double Left(int column) => Math.Max(0, column) * ColumnPitch;

    public static double GroupVisualWidth(int widthUnits) =>
        RequiredWidth(Math.Clamp(widthUnits, MinimumGroupWidthUnits, MaximumGroupWidthUnits));

    public static int TileColumns(int widthUnits) =>
        Math.Clamp(widthUnits, MinimumGroupWidthUnits, MaximumGroupWidthUnits) * TileSize.Medium.ColumnSpan();

    public static int TileRows(int heightUnits) =>
        Math.Clamp(heightUnits, MinimumGroupHeightUnits, MaximumGroupHeightUnits) * TileSize.Medium.RowSpan();
}