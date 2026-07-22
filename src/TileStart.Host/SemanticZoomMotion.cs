using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Size = System.Windows.Size;

namespace TileStart.Host;

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
    public const int PresenterFadeDurationMilliseconds = 167;

    // DirectManipulation owns the native duration/easing and does not expose them through ZoomToRect.
    // Keep this isolated as the one Win10-video calibration value in the WPF equivalent.
    public const int ViewChangeDurationMilliseconds = 350;

    private const int DesiredFrameRate = 240;
    private static readonly KeySpline ViewChangeSpline = new(0.15, 0.75, 0.25, 1);

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
        ClearAnimationCache(zoomedInPresenter, zoomedOutPresenter);
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

        ClearAnimations(sharedScale, sharedTranslate, zoomedInPresenter, zoomedOutPresenter);
        EnableAnimationCache(zoomedInPresenter, zoomedOutPresenter);
        ApplySurfaceState(target, sharedScale, sharedTranslate);
        zoomedInPresenter.Opacity = targetZoomedInOpacity;
        zoomedOutPresenter.Opacity = targetZoomedOutOpacity;

        var scaleXAnimation = CreateViewChangeAnimation(fromScaleX, target.Scale);
        scaleXAnimation.Completed += (_, _) =>
        {
            ClearAnimationCache(zoomedInPresenter, zoomedOutPresenter);
            completed();
        };
        sharedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation, HandoffBehavior.SnapshotAndReplace);
        sharedScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            CreateViewChangeAnimation(fromScaleY, target.Scale),
            HandoffBehavior.SnapshotAndReplace);
        sharedTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            CreateViewChangeAnimation(fromTranslateX, target.TranslateX),
            HandoffBehavior.SnapshotAndReplace);
        sharedTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            CreateViewChangeAnimation(fromTranslateY, target.TranslateY),
            HandoffBehavior.SnapshotAndReplace);
        zoomedInPresenter.BeginAnimation(
            UIElement.OpacityProperty,
            CreateFadeAnimation(fromZoomedInOpacity, targetZoomedInOpacity),
            HandoffBehavior.SnapshotAndReplace);
        zoomedOutPresenter.BeginAnimation(
            UIElement.OpacityProperty,
            CreateFadeAnimation(fromZoomedOutOpacity, targetZoomedOutOpacity),
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

    private static void ClearAnimationCache(UIElement zoomedInPresenter, UIElement zoomedOutPresenter)
    {
        zoomedInPresenter.CacheMode = null;
        zoomedOutPresenter.CacheMode = null;
    }

    private static DoubleAnimationUsingKeyFrames CreateViewChangeAnimation(double from, double to)
    {
        var duration = TimeSpan.FromMilliseconds(ViewChangeDurationMilliseconds);
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            FillBehavior = FillBehavior.Stop,
        };
        Timeline.SetDesiredFrameRate(animation, DesiredFrameRate);
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration), ViewChangeSpline));
        return animation;
    }

    private static DoubleAnimation CreateFadeAnimation(double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(PresenterFadeDurationMilliseconds),
            FillBehavior = FillBehavior.Stop,
        };
        Timeline.SetDesiredFrameRate(animation, DesiredFrameRate);
        return animation;
    }
}