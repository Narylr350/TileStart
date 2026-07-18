using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TileStart.Host;

public static class Win10ReorderMotion
{
    public const int DurationMilliseconds = 400;
    public static KeySpline Spline { get; } = new(0.1, 0.9, 0.2, 1);

    public static void AnimateFrom(FrameworkElement element, Vector delta)
    {
        if (Math.Abs(delta.X) < 0.1 && Math.Abs(delta.Y) < 0.1)
        {
            return;
        }

        var transform = new TranslateTransform(delta.X, delta.Y);
        element.RenderTransform = transform;
        var duration = TimeSpan.FromMilliseconds(DurationMilliseconds);
        var x = Create(delta.X, duration);
        var y = Create(delta.Y, duration);
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

    private static DoubleAnimationUsingKeyFrames Create(double from, TimeSpan duration)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = duration };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration), Spline));
        return animation;
    }
}
