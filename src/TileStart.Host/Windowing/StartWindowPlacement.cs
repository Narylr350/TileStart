namespace TileStart.Host.Windowing;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
}

public static class StartWindowPlacement
{
    public static TaskbarEdge InferTaskbarEdge(PixelRect monitor, PixelRect? taskbar)
    {
        if (taskbar is not { } rect)
        {
            return TaskbarEdge.Bottom;
        }

        if (rect.Width >= rect.Height)
        {
            return Math.Abs(rect.Top - monitor.Top) <= Math.Abs(rect.Bottom - monitor.Bottom)
                ? TaskbarEdge.Top
                : TaskbarEdge.Bottom;
        }

        return Math.Abs(rect.Left - monitor.Left) <= Math.Abs(rect.Right - monitor.Right)
            ? TaskbarEdge.Left
            : TaskbarEdge.Right;
    }

    public static PixelRect Calculate(PixelRect workArea, TaskbarEdge edge, int requestedWidth, int requestedHeight)
    {
        var width = Math.Min(Math.Max(1, requestedWidth), workArea.Width);
        var height = Math.Min(Math.Max(1, requestedHeight), workArea.Height);
        var left = edge == TaskbarEdge.Right ? workArea.Right - width : workArea.Left;
        var top = edge == TaskbarEdge.Bottom ? workArea.Bottom - height : workArea.Top;
        return new PixelRect(left, top, left + width, top + height);
    }
}
