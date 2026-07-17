using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TileStart.Host;

public readonly record struct EntranceMotionParameters(
    double FromY,
    int DelayMilliseconds,
    int DurationMilliseconds);

public static class StartMotion
{
    private static readonly KeySpline EntranceSpline = new(0.1, 0.9, 0.2, 1);

    private static readonly DependencyProperty MotionTranslateProperty = DependencyProperty.RegisterAttached(
        "MotionTranslate",
        typeof(TranslateTransform),
        typeof(StartMotion));

    private static readonly DependencyProperty PreparedFromYProperty = DependencyProperty.RegisterAttached(
        "PreparedFromY",
        typeof(double),
        typeof(StartMotion),
        new PropertyMetadata(double.NaN));

    public static EntranceMotionParameters CalculateEntrance(double normalizedY, bool bottomTaskbar)
    {
        var position = Math.Clamp(normalizedY, 0, 1);
        return new EntranceMotionParameters(
            bottomTaskbar ? (position * 110) + 60 : 0,
            bottomTaskbar ? 17 : 0,
            bottomTaskbar ? 500 : 0);
    }

    public static void StageEntrance(
        FrameworkElement root,
        IEnumerable<FrameworkElement> elements,
        bool bottomTaskbar,
        bool animationsEnabled)
    {
        if (root.ActualHeight <= 0 || root.ActualWidth <= 0)
        {
            return;
        }

        var rootBounds = new Rect(0, 0, root.ActualWidth, root.ActualHeight);
        foreach (var element in elements)
        {
            var translate = EnsureTransform(element);
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = 0;
            element.SetValue(PreparedFromYProperty, double.NaN);

            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                continue;
            }

            var origin = ReferenceEquals(element, root)
                ? new System.Windows.Point()
                : element.TransformToAncestor(root).Transform(new System.Windows.Point());
            if (!rootBounds.IntersectsWith(new Rect(origin, element.RenderSize)))
            {
                continue;
            }

            var parameters = CalculateEntrance(origin.Y / root.ActualHeight, bottomTaskbar);
            var fromY = animationsEnabled ? parameters.FromY : 0;
            element.SetValue(PreparedFromYProperty, fromY);
            translate.Y = fromY;
        }
    }

    public static void PlayEntrance(IEnumerable<FrameworkElement> elements, bool animationsEnabled)
    {
        foreach (var element in elements)
        {
            var fromY = (double)element.GetValue(PreparedFromYProperty);
            if (double.IsNaN(fromY))
            {
                continue;
            }

            var translate = EnsureTransform(element);
            translate.Y = 0;
            element.ClearValue(PreparedFromYProperty);
            var parameters = CalculateEntrance(0, fromY > 0);
            if (!animationsEnabled || parameters.DurationMilliseconds == 0)
            {
                continue;
            }

            var animation = CreateSplineAnimation(
                fromY,
                0,
                parameters.DelayMilliseconds,
                parameters.DurationMilliseconds,
                EntranceSpline);
            translate.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
    }

    public static void Prepare(IEnumerable<FrameworkElement> elements)
    {
        foreach (var element in elements)
        {
            EnsureTransform(element);
        }
    }

    private static TranslateTransform EnsureTransform(FrameworkElement element)
    {
        if (element.GetValue(MotionTranslateProperty) is TranslateTransform translate)
        {
            return translate;
        }

        translate = new TranslateTransform();
        element.SetValue(MotionTranslateProperty, translate);
        element.RenderTransform = translate;
        return translate;
    }

    private static DoubleAnimationUsingKeyFrames CreateSplineAnimation(
        double from,
        double to,
        int delayMilliseconds,
        int durationMilliseconds,
        KeySpline spline)
    {
        var delay = TimeSpan.FromMilliseconds(delayMilliseconds);
        var end = TimeSpan.FromMilliseconds(delayMilliseconds + durationMilliseconds);
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = end,
            FillBehavior = FillBehavior.Stop,
        };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(end), spline));
        return animation;
    }
}
