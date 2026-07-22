using System.Windows;
using System.Windows.Media.Animation;

namespace TileStart.Host;

public static class Win10MenuPopupMotion
{
    public const double TopLevelClosedRatio = 0.5;
    public const double SubmenuClosedRatio = 0.67;
    public const int OpenDurationMilliseconds = 250;
    public const int CloseOpacityDurationMilliseconds = 83;

    public static KeySpline OpenSpline { get; } = new(0, 0, 0, 1);

    public static Rect InitialClip(
        double width,
        double height,
        double closedRatio,
        bool popupOpensUpward,
        double? pointerOriginY = null,
        bool useSubmenuDirection = false)
    {
        var visibleHeight = Math.Max(0, height) * (1 - Math.Clamp(closedRatio, 0, 1));
        if (pointerOriginY is { } originY)
        {
            if (popupOpensUpward)
            {
                var top = Math.Clamp(originY, 0, Math.Max(0, height - visibleHeight));
                return new Rect(0, top, Math.Max(0, width), Math.Max(0, height - top));
            }

            var bottom = Math.Clamp(originY, visibleHeight, Math.Max(visibleHeight, height));
            return new Rect(0, 0, Math.Max(0, width), bottom);
        }

        if (useSubmenuDirection)
        {
            return popupOpensUpward
                ? new Rect(0, Math.Max(0, height) - visibleHeight, Math.Max(0, width), visibleHeight)
                : new Rect(0, 0, Math.Max(0, width), visibleHeight);
        }

        return popupOpensUpward
            ? new Rect(0, 0, Math.Max(0, width), visibleHeight)
            : new Rect(0, Math.Max(0, height) - visibleHeight, Math.Max(0, width), visibleHeight);
    }

    public static RectAnimationUsingKeyFrames CreateOpenAnimation(
        double width,
        double height,
        double closedRatio,
        bool popupOpensUpward,
        double? pointerOriginY = null,
        bool useSubmenuDirection = false)
    {
        var duration = TimeSpan.FromMilliseconds(OpenDurationMilliseconds);
        var animation = new RectAnimationUsingKeyFrames
        {
            Duration = duration,
            FillBehavior = FillBehavior.Stop,
        };
        Timeline.SetDesiredFrameRate(animation, StartMotion.DesiredFrameRate);
        animation.KeyFrames.Add(new DiscreteRectKeyFrame(
            InitialClip(width, height, closedRatio, popupOpensUpward, pointerOriginY, useSubmenuDirection),
            KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineRectKeyFrame(
            new Rect(0, 0, Math.Max(0, width), Math.Max(0, height)),
            KeyTime.FromTimeSpan(duration),
            OpenSpline));
        return animation;
    }
}
