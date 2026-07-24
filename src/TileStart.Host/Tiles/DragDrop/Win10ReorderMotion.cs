using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TileStart.Host.Tiles.DragDrop;

public static class Win10ReorderMotion
{
    public const int DurationMilliseconds = 400;
    public const int DropHandoffDurationMilliseconds = 50;

    // No isolated Win10 cancel recording is available yet. Keep this as an explicit
    // TileStart approximation until it can be calibrated against an original sample.
    public const int CancelReturnDurationMilliseconds = 200;

    public static KeySpline Spline { get; } = new(0.1, 0.9, 0.2, 1);

    public static Vector ResolveRetargetDelta(
        System.Windows.Point previousVisiblePosition,
        System.Windows.Point currentVisiblePosition,
        Vector activeTranslation) =>
        previousVisiblePosition - (currentVisiblePosition - activeTranslation);

    public static void AnimateFrom(FrameworkElement element, Vector delta)
    {
        if (Math.Abs(delta.X) < 0.1 && Math.Abs(delta.Y) < 0.1)
        {
            return;
        }

        var transform = new TranslateTransform(delta.X, delta.Y);
        element.RenderTransform = transform;
        var duration = TimeSpan.FromMilliseconds(DurationMilliseconds);
        var x = Create(delta.X, 0, duration);
        var y = Create(delta.Y, 0, duration);
        y.Completed += (_, _) =>
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.X = 0;
            transform.Y = 0;
        };
        transform.BeginAnimation(TranslateTransform.XProperty, x, HandoffBehavior.SnapshotAndReplace);
        transform.BeginAnimation(TranslateTransform.YProperty, y, HandoffBehavior.SnapshotAndReplace);
    }

    public static DoubleAnimationUsingKeyFrames Create(double from, double to, TimeSpan duration)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration), Spline));
        return animation;
    }
}