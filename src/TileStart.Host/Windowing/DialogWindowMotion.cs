using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TileStart.Host.Windowing;

public static class DialogWindowMotion
{
    public const int OpenDurationMilliseconds = 320;
    public const int CloseDurationMilliseconds = 180;
    public const double OpenOffset = 8;
    public const double CloseOffset = 5;

    public static KeySpline OpenSpline { get; } = new(0.16, 0.8, 0.25, 1);
    public static KeySpline CloseSpline { get; } = new(0.7, 0, 1, 0.5);

    private static readonly ConditionalWeakTable<Window, MotionState> States = new();

    public static readonly DependencyProperty IsCloseAnimationEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsCloseAnimationEnabled",
            typeof(bool),
            typeof(DialogWindowMotion),
            new FrameworkPropertyMetadata(true));

    private sealed class MotionState
    {
        public bool IsAttached { get; set; }
        public bool AllowClose { get; set; }
        public bool IsClosing { get; set; }
        public TranslateTransform? Translation { get; set; }
    }

    public static void SetIsCloseAnimationEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsCloseAnimationEnabledProperty, value);

    public static bool GetIsCloseAnimationEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsCloseAnimationEnabledProperty);

    public static void Open(Window window)
    {
        var state = States.GetValue(window, static _ => new MotionState());
        if (!state.IsAttached)
        {
            state.IsAttached = true;
            window.Closing += Window_Closing;
        }

        window.BeginAnimation(UIElement.OpacityProperty, null);
        if (!SystemParameters.ClientAreaAnimation)
        {
            window.Opacity = 1;
            return;
        }

        state.Translation = PrepareTranslation(window, OpenOffset);
        window.Opacity = 0;
        window.BeginAnimation(
            UIElement.OpacityProperty,
            CreateAnimation(0, 1, OpenDurationMilliseconds, OpenSpline),
            HandoffBehavior.SnapshotAndReplace);
        state.Translation?.BeginAnimation(
            TranslateTransform.YProperty,
            CreateAnimation(OpenOffset, 0, OpenDurationMilliseconds, OpenSpline),
            HandoffBehavior.SnapshotAndReplace);
    }

    public static void BeginClose(Window window, CancelEventArgs e)
    {
        var state = States.GetValue(window, static _ => new MotionState());
        if (state.AllowClose
            || !GetIsCloseAnimationEnabled(window)
            || !SystemParameters.ClientAreaAnimation
            || System.Windows.Application.Current?.Dispatcher.HasShutdownStarted == true
            || !window.IsVisible)
        {
            return;
        }

        e.Cancel = true;
        if (state.IsClosing)
        {
            return;
        }

        state.IsClosing = true;
        state.Translation ??= PrepareTranslation(window, 0);
        var fade = CreateAnimation(window.Opacity, 0, CloseDurationMilliseconds, CloseSpline);
        fade.Completed += (_, _) =>
        {
            state.AllowClose = true;
            window.Close();
        };
        window.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        state.Translation?.BeginAnimation(
            TranslateTransform.YProperty,
            CreateAnimation(0, CloseOffset, CloseDurationMilliseconds, CloseSpline),
            HandoffBehavior.SnapshotAndReplace);
    }

    internal static DoubleAnimationUsingKeyFrames CreateAnimation(
        double from,
        double to,
        int durationMilliseconds,
        KeySpline spline)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Max(0, durationMilliseconds));
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            FillBehavior = FillBehavior.HoldEnd,
        };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(duration), spline));
        return animation;
    }

    private static void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (sender is Window window)
        {
            BeginClose(window, e);
        }
    }

    private static TranslateTransform? PrepareTranslation(Window window, double initialY)
    {
        if (window.Content is not FrameworkElement content)
        {
            return null;
        }

        if (content.RenderTransform is TranslateTransform translation)
        {
            translation.BeginAnimation(TranslateTransform.YProperty, null);
            translation.Y = initialY;
            return translation;
        }

        if (content.RenderTransform is not null && !content.RenderTransform.Value.IsIdentity)
        {
            return null;
        }

        translation = new TranslateTransform(0, initialY);
        content.RenderTransform = translation;
        return translation;
    }
}
