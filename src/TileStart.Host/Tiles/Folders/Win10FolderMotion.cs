using System.Windows.Media.Animation;

namespace TileStart.Host.Tiles.Folders;

public static class Win10FolderMotion
{
    public const int AppChildDurationMilliseconds = 167;
    public const int AppChildDelayStepMilliseconds = 17;
    public const int AppReflowDurationMilliseconds = 400;

    public const int TileExpandShiftBaseDurationMilliseconds = 100;
    public const int TileExpandRowBudgetMilliseconds = 10;
    public const int TileExpandColumnBudgetMilliseconds = 5;
    public const int TileCollapseShiftBaseDurationMilliseconds = 300;
    public const int TileCollapseRowBudgetMilliseconds = 150;
    public const int TileCollapseColumnBudgetMilliseconds = 150;

    public const int TilePreviewExitDurationMilliseconds = 180;
    public const int TilePreviewEnterDurationMilliseconds = 240;
    public const int TileChildDurationMilliseconds = 300;
    public const int TileChildWaveDelayMilliseconds = 30;
    public const int TileDecorationDelayMilliseconds = 280;
    public const int TileDecorationDurationMilliseconds = 100;

    public static KeySpline StandardSpline { get; } = CreateFrozenSpline(0.1, 0.9, 0.2, 1);
    public static KeySpline TileExpandShiftSpline { get; } = CreateFrozenSpline(0.9, 0.1, 1, 0.2);

    public static int AppChildDelay(int index) => Math.Max(0, index) * AppChildDelayStepMilliseconds;

    public static int AppOpenDuration(int childCount) =>
        Math.Max(
            AppReflowDurationMilliseconds,
            AppChildrenDuration(childCount));

    public static int AppChildrenDuration(int childCount) =>
        AppChildDurationMilliseconds + AppChildDelay(Math.Max(0, childCount - 1));

    public static int TileChildWaveDelay(
        int rowIndex,
        int columnIndex,
        int rowCount,
        int columnCount)
    {
        var lastRow = Math.Max(0, rowCount - 1);
        var lastColumn = Math.Max(0, columnCount - 1);
        return ((lastRow - Math.Clamp(rowIndex, 0, lastRow))
                + (lastColumn - Math.Clamp(columnIndex, 0, lastColumn)))
               * TileChildWaveDelayMilliseconds;
    }

    public static int TileChildrenDuration(int rowCount, int columnCount) =>
        rowCount <= 0 || columnCount <= 0
            ? 0
            : TileChildDurationMilliseconds
              + TileChildWaveDelay(0, 0, rowCount, columnCount);

    private static KeySpline CreateFrozenSpline(double x1, double y1, double x2, double y2)
    {
        var spline = new KeySpline(x1, y1, x2, y2);
        spline.Freeze();
        return spline;
    }

    public static int TileShiftDuration(
        bool expanding,
        int row,
        int column,
        int rowCount,
        int columnCount)
    {
        var safeRows = Math.Max(2, rowCount);
        var safeColumns = Math.Max(2, columnCount);
        var safeRow = Math.Clamp(row, 0, safeRows - 1);
        var safeColumn = Math.Clamp(column, 0, safeColumns - 1);

        if (expanding)
        {
            var rowStep = TileExpandRowBudgetMilliseconds / (safeRows - 1);
            var columnStep = TileExpandColumnBudgetMilliseconds / (safeColumns - 1);
            return TileExpandShiftBaseDurationMilliseconds
                   + ((safeRows - safeRow - 1) * rowStep)
                   + ((safeColumns - safeColumn - 1) * columnStep);
        }

        var collapseRowStep = TileCollapseRowBudgetMilliseconds / (safeRows - 1);
        var collapseColumnStep = TileCollapseColumnBudgetMilliseconds / (safeColumns - 1);
        return TileCollapseShiftBaseDurationMilliseconds
               + (safeRow * collapseRowStep)
               + (safeColumn * collapseColumnStep);
    }

    public static DoubleAnimationUsingKeyFrames CreateSplineAnimation(
        double from,
        double to,
        int delayMilliseconds,
        int durationMilliseconds,
        KeySpline spline,
        FillBehavior fillBehavior = FillBehavior.Stop)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, delayMilliseconds));
        var end = delay + TimeSpan.FromMilliseconds(Math.Max(0, durationMilliseconds));
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = end,
            FillBehavior = fillBehavior,
        };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(end), spline));
        return animation;
    }
}