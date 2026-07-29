using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Size = System.Windows.Size;

namespace TileStart.Host.Navigation;

public readonly record struct SemanticZoomTransform(double Scale, double TranslateX, double TranslateY)
{
    public Rect Transform(Rect bounds) => new(
        (bounds.X * Scale) + TranslateX,
        (bounds.Y * Scale) + TranslateY,
        bounds.Width * Scale,
        bounds.Height * Scale);
}

public static class SemanticZoomMotion
{
    public const double ZoomedInPresenterScale = 0.5;
    public const int PresenterFadeDurationMilliseconds = 240;
    public const int ZoomOutGeometryDurationMilliseconds = 500;
    public const int ZoomInGeometryDurationMilliseconds = 500;

    private static readonly SemanticZoomProgressPoint[] ZoomOutGeometryProgress =
    [
        new(0, 0),
        new(0.15, 0.39),
        new(0.35, 0.73),
        new(0.60, 0.94),
        new(0.82, 0.99),
        new(1, 1),
    ];

    private static readonly SemanticZoomProgressPoint[] ZoomInGeometryProgress =
    [
        new(0, 0),
        new(0.15, 0.39),
        new(0.35, 0.73),
        new(0.60, 0.94),
        new(0.82, 0.99),
        new(1, 1),
    ];

    public static SemanticZoomTransform CalculateSurfaceState(Size viewport, bool zoomedInViewActive)
    {
        if (!zoomedInViewActive)
        {
            return new SemanticZoomTransform(1, 0, 0);
        }

        return new SemanticZoomTransform(
            1 / ZoomedInPresenterScale,
            -viewport.Width / 2,
            -viewport.Height / 2);
    }

    public static SemanticZoomTransform CalculateZoomedInPresenterCorrection(Size viewport) => new(
        ZoomedInPresenterScale,
        viewport.Width / 4,
        viewport.Height / 4);

    public static double CalculateGeometryProgress(bool zoomedInViewActive, double normalizedTime)
    {
        var time = Math.Clamp(normalizedTime, 0, 1);
        var points = zoomedInViewActive ? ZoomInGeometryProgress : ZoomOutGeometryProgress;
        for (var index = 1; index < points.Length; index++)
        {
            var right = points[index];
            if (time > right.Time)
            {
                continue;
            }

            var left = points[index - 1];
            var segmentProgress = (time - left.Time) / (right.Time - left.Time);
            return left.Progress + ((right.Progress - left.Progress) * segmentProgress);
        }

        return 1;
    }

    public static void Snap(
        Size viewport,
        bool zoomedInViewActive,
        ScaleTransform sharedScale,
        TranslateTransform sharedTranslate,
        ScaleTransform zoomedInScale,
        TranslateTransform zoomedInTranslate,
        UIElement zoomedInPresenter,
        UIElement zoomedOutPresenter)
    {
        ClearAnimations(sharedScale, sharedTranslate, zoomedInPresenter, zoomedOutPresenter);
        ClearTransientEffects(zoomedInPresenter, zoomedOutPresenter);
        ApplyPresenterCorrection(viewport, zoomedInScale, zoomedInTranslate);
        ApplySurfaceState(CalculateSurfaceState(viewport, zoomedInViewActive), sharedScale, sharedTranslate);
        zoomedInPresenter.Opacity = zoomedInViewActive ? 1 : 0;
        zoomedOutPresenter.Opacity = zoomedInViewActive ? 0 : 1;
    }

    public static void Animate(
        Size viewport,
        bool zoomedInViewActive,
        ScaleTransform sharedScale,
        TranslateTransform sharedTranslate,
        ScaleTransform zoomedInScale,
        TranslateTransform zoomedInTranslate,
        UIElement zoomedInPresenter,
        UIElement zoomedOutPresenter,
        bool animationsEnabled,
        Action completed)
    {
        ApplyPresenterCorrection(viewport, zoomedInScale, zoomedInTranslate);
        if (!animationsEnabled)
        {
            Snap(
                viewport,
                zoomedInViewActive,
                sharedScale,
                sharedTranslate,
                zoomedInScale,
                zoomedInTranslate,
                zoomedInPresenter,
                zoomedOutPresenter);
            completed();
            return;
        }

        var fromScaleX = sharedScale.ScaleX;
        var fromScaleY = sharedScale.ScaleY;
        var fromTranslateX = sharedTranslate.X;
        var fromTranslateY = sharedTranslate.Y;
        var fromZoomedInOpacity = zoomedInPresenter.Opacity;
        var fromZoomedOutOpacity = zoomedOutPresenter.Opacity;
        var target = CalculateSurfaceState(viewport, zoomedInViewActive);
        var targetZoomedInOpacity = zoomedInViewActive ? 1d : 0d;
        var targetZoomedOutOpacity = zoomedInViewActive ? 0d : 1d;
        var timing = CalculateTiming(zoomedInViewActive);

        ClearAnimations(sharedScale, sharedTranslate, zoomedInPresenter, zoomedOutPresenter);
        EnableAnimationCache(zoomedInPresenter, zoomedOutPresenter);
        ApplySurfaceState(target, sharedScale, sharedTranslate);
        zoomedInPresenter.Opacity = targetZoomedInOpacity;
        zoomedOutPresenter.Opacity = targetZoomedOutOpacity;

        var scaleXAnimation = CreateViewChangeAnimation(fromScaleX, target.Scale, timing);
        var scaleYAnimation = CreateViewChangeAnimation(fromScaleY, target.Scale, timing);
        scaleYAnimation.Completed += (_, _) =>
        {
            ClearTransientEffects(zoomedInPresenter, zoomedOutPresenter);
            completed();
        };
        sharedScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            scaleXAnimation,
            HandoffBehavior.SnapshotAndReplace);
        sharedScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            scaleYAnimation,
            HandoffBehavior.SnapshotAndReplace);
        sharedTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateViewChangeAnimation(fromTranslateX, target.TranslateX, timing),
            HandoffBehavior.SnapshotAndReplace);
        sharedTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            CreateViewChangeAnimation(fromTranslateY, target.TranslateY, timing),
            HandoffBehavior.SnapshotAndReplace);
        zoomedInPresenter.BeginAnimation(
            UIElement.OpacityProperty,
            CreatePresenterOpacityAnimation(
                fromZoomedInOpacity,
                targetZoomedInOpacity),
            HandoffBehavior.SnapshotAndReplace);
        zoomedOutPresenter.BeginAnimation(
            UIElement.OpacityProperty,
            CreatePresenterOpacityAnimation(
                fromZoomedOutOpacity,
                targetZoomedOutOpacity),
            HandoffBehavior.SnapshotAndReplace);
    }

    private static void ApplyPresenterCorrection(
        Size viewport,
        ScaleTransform zoomedInScale,
        TranslateTransform zoomedInTranslate)
    {
        var correction = CalculateZoomedInPresenterCorrection(viewport);
        zoomedInScale.ScaleX = correction.Scale;
        zoomedInScale.ScaleY = correction.Scale;
        zoomedInTranslate.X = correction.TranslateX;
        zoomedInTranslate.Y = correction.TranslateY;
    }

    private static void ApplySurfaceState(
        SemanticZoomTransform state,
        ScaleTransform sharedScale,
        TranslateTransform sharedTranslate)
    {
        sharedScale.ScaleX = state.Scale;
        sharedScale.ScaleY = state.Scale;
        sharedTranslate.X = state.TranslateX;
        sharedTranslate.Y = state.TranslateY;
    }

    private static void ClearAnimations(
        ScaleTransform sharedScale,
        TranslateTransform sharedTranslate,
        UIElement zoomedInPresenter,
        UIElement zoomedOutPresenter)
    {
        sharedScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        sharedScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        sharedTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        sharedTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        zoomedInPresenter.BeginAnimation(UIElement.OpacityProperty, null);
        zoomedOutPresenter.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private static void EnableAnimationCache(UIElement zoomedInPresenter, UIElement zoomedOutPresenter)
    {
        zoomedInPresenter.CacheMode = new BitmapCache { RenderAtScale = 1 };
        zoomedOutPresenter.CacheMode = new BitmapCache { RenderAtScale = 1 };
    }

    private static void ClearTransientEffects(UIElement zoomedInPresenter, UIElement zoomedOutPresenter)
    {
        zoomedInPresenter.CacheMode = null;
        zoomedOutPresenter.CacheMode = null;
        zoomedInPresenter.Effect = null;
        zoomedOutPresenter.Effect = null;
    }

    private static SemanticZoomTiming CalculateTiming(bool zoomedInViewActive) => zoomedInViewActive
        ? new SemanticZoomTiming(
            0,
            ZoomInGeometryDurationMilliseconds,
            ZoomInGeometryProgress)
        : new SemanticZoomTiming(
            0,
            ZoomOutGeometryDurationMilliseconds,
            ZoomOutGeometryProgress);

    private static DoubleAnimationUsingKeyFrames CreateViewChangeAnimation(
        double from,
        double to,
        SemanticZoomTiming timing)
    {
        var delay = TimeSpan.FromMilliseconds(timing.GeometryDelayMilliseconds);
        var end = TimeSpan.FromMilliseconds(timing.GeometryDelayMilliseconds + timing.GeometryDurationMilliseconds);
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = end,
            FillBehavior = FillBehavior.Stop,
        };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if (timing.GeometryDelayMilliseconds > 0)
        {
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)));
        }

        foreach (var point in timing.GeometryProgress.Skip(1))
        {
            var keyTime = TimeSpan.FromMilliseconds(
                timing.GeometryDelayMilliseconds + (timing.GeometryDurationMilliseconds * point.Time));
            var value = from + ((to - from) * point.Progress);
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(value, KeyTime.FromTimeSpan(keyTime)));
        }

        return animation;
    }

    private static AnimationTimeline CreatePresenterOpacityAnimation(
        double from,
        double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(PresenterFadeDurationMilliseconds),
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        return animation;
    }

    private readonly record struct SemanticZoomTiming(
        int GeometryDelayMilliseconds,
        int GeometryDurationMilliseconds,
        IReadOnlyList<SemanticZoomProgressPoint> GeometryProgress);

    private readonly record struct SemanticZoomProgressPoint(double Time, double Progress);
}