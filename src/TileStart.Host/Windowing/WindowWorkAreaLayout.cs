namespace TileStart.Host.Windowing;

public readonly record struct LogicalWindowSize(double Width, double Height);

public static class WindowWorkAreaLayout
{
    public const double SafeInset = 12;

    public static LogicalWindowSize FitSize(
        LogicalWindowSize desired,
        LogicalWindowSize minimum,
        LogicalWindowSize workArea,
        double safeInset = SafeInset)
    {
        var availableWidth = Math.Max(1, workArea.Width - safeInset * 2);
        var availableHeight = Math.Max(1, workArea.Height - safeInset * 2);
        var minimumWidth = Math.Min(NormalizeMinimum(minimum.Width), availableWidth);
        var minimumHeight = Math.Min(NormalizeMinimum(minimum.Height), availableHeight);
        var desiredWidth = NormalizeDesired(desired.Width, minimumWidth);
        var desiredHeight = NormalizeDesired(desired.Height, minimumHeight);

        return new LogicalWindowSize(
            Math.Clamp(desiredWidth, minimumWidth, availableWidth),
            Math.Clamp(desiredHeight, minimumHeight, availableHeight));
    }

    public static PixelRect CenterAndClamp(
        PixelRect workArea,
        PixelRect? owner,
        int requestedWidth,
        int requestedHeight)
    {
        var width = Math.Min(Math.Max(1, requestedWidth), workArea.Width);
        var height = Math.Min(Math.Max(1, requestedHeight), workArea.Height);
        var anchor = owner is { } ownerRect ? Intersect(ownerRect, workArea) : workArea;
        if (anchor.Width == 0 || anchor.Height == 0)
        {
            anchor = workArea;
        }

        var left = anchor.Left + (anchor.Width - width) / 2;
        var top = anchor.Top + (anchor.Height - height) / 2;
        left = Math.Clamp(left, workArea.Left, workArea.Right - width);
        top = Math.Clamp(top, workArea.Top, workArea.Bottom - height);
        return new PixelRect(left, top, left + width, top + height);
    }

    private static PixelRect Intersect(PixelRect first, PixelRect second) =>
        new(
            Math.Max(first.Left, second.Left),
            Math.Max(first.Top, second.Top),
            Math.Min(first.Right, second.Right),
            Math.Min(first.Bottom, second.Bottom));

    private static double NormalizeMinimum(double value) =>
        double.IsFinite(value) && value > 0 ? value : 1;

    private static double NormalizeDesired(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;
}
