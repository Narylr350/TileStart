using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TileStart.Host.Utilities;

public static class SmoothScroll
{
    private const double DefaultLineHeight = 36;
    private const double StopDistance = 0.9;
    private const double TimeConstantMilliseconds = 34;
    private const double MaximumSettleMilliseconds = 150;

    private static readonly ConditionalWeakTable<FrameworkElement, ScrollState> States = new();

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SmoothScroll),
        new PropertyMetadata(false, IsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static void Cancel(DependencyObject element)
    {
        if (element is FrameworkElement owner && States.TryGetValue(owner, out var state))
        {
            state.Cancel();
        }
    }

    internal static double CalculateWheelDistance(int wheelScrollLines, double viewportHeight)
    {
        if (wheelScrollLines < 0)
        {
            return Math.Max(DefaultLineHeight, viewportHeight * 0.85);
        }

        return Math.Max(1, wheelScrollLines) * DefaultLineHeight;
    }

    internal static double InterpolateOffset(double current, double target, double elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= 0 || current == target)
        {
            return current;
        }

        var progress = 1 - Math.Exp(-elapsedMilliseconds / TimeConstantMilliseconds);
        return current + ((target - current) * progress);
    }

    internal static bool ShouldSnapToTarget(double remainingDistance, double millisecondsSinceLastInput) =>
        remainingDistance <= StopDistance || millisecondsSinceLastInput >= MaximumSettleMilliseconds;

    private static void IsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            element.PreviewMouseWheel += PreviewMouseWheel;
            element.Unloaded += ElementUnloaded;
        }
        else
        {
            element.PreviewMouseWheel -= PreviewMouseWheel;
            element.Unloaded -= ElementUnloaded;
            if (States.TryGetValue(element, out var state))
            {
                state.Dispose();
                States.Remove(element);
            }
        }
    }

    private static void PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not FrameworkElement owner || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var selectedOwner = FindScrollableOwner(source, e.Delta);
        if (!ReferenceEquals(selectedOwner, owner))
        {
            return;
        }

        var state = States.GetValue(owner, static element => new ScrollState(element));
        if (state.Scroll(e.Delta))
        {
            e.Handled = true;
        }
    }

    private static FrameworkElement? FindScrollableOwner(DependencyObject source, int wheelDelta)
    {
        for (DependencyObject? current = source; current is not null; current = GetParent(current))
        {
            if (current is not FrameworkElement owner || !GetIsEnabled(owner))
            {
                continue;
            }

            var state = States.GetValue(owner, static element => new ScrollState(element));
            if (state.CanScroll(wheelDelta))
            {
                return owner;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        return element is FrameworkElement frameworkElement
            ? frameworkElement.Parent ?? frameworkElement.TemplatedParent
            : null;
    }

    private static void ElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && States.TryGetValue(element, out var state))
        {
            state.Dispose();
            States.Remove(element);
        }
    }

    private sealed class ScrollState : IDisposable
    {
        private readonly FrameworkElement _owner;
        private ScrollViewer? _viewer;
        private double _targetOffset;
        private long _lastRenderingTimestamp;
        private long _lastInputTimestamp;
        private bool _isAnimating;

        public ScrollState(FrameworkElement owner)
        {
            _owner = owner;
        }

        public bool CanScroll(int wheelDelta)
        {
            var viewer = GetViewer();
            if (viewer is null || viewer.ScrollableHeight <= 0)
            {
                return false;
            }

            var offset = _isAnimating ? _targetOffset : viewer.VerticalOffset;
            return wheelDelta > 0 ? offset > StopDistance : offset < viewer.ScrollableHeight - StopDistance;
        }

        public bool Scroll(int wheelDelta)
        {
            var viewer = GetViewer();
            if (viewer is null || viewer.ScrollableHeight <= 0)
            {
                return false;
            }

            var distance = CalculateWheelDistance(SystemParameters.WheelScrollLines, viewer.ViewportHeight);
            var currentTarget = _isAnimating ? _targetOffset : viewer.VerticalOffset;
            var nextTarget = Math.Clamp(
                currentTarget - ((wheelDelta / 120d) * distance),
                0,
                viewer.ScrollableHeight);
            if (Math.Abs(nextTarget - currentTarget) < StopDistance)
            {
                return false;
            }

            _targetOffset = nextTarget;
            _lastInputTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!SystemParameters.ClientAreaAnimation)
            {
                viewer.ScrollToVerticalOffset(_targetOffset);
                return true;
            }

            if (!_isAnimating)
            {
                _isAnimating = true;
                _lastRenderingTimestamp = 0;
                CompositionTarget.Rendering += CompositionTargetRendering;
            }

            return true;
        }

        public void Cancel() => StopAnimation();

        public void Dispose()
        {
            StopAnimation();
            _viewer = null;
        }

        private ScrollViewer? GetViewer()
        {
            if (_viewer is not null)
            {
                return _viewer;
            }

            if (_owner is System.Windows.Controls.Control control)
            {
                control.ApplyTemplate();
            }

            _viewer = _owner as ScrollViewer ?? FindVisualDescendant<ScrollViewer>(_owner);
            return _viewer;
        }

        private void CompositionTargetRendering(object? sender, EventArgs e)
        {
            var viewer = GetViewer();
            if (viewer is null)
            {
                StopAnimation();
                return;
            }

            var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_lastRenderingTimestamp == 0)
            {
                _lastRenderingTimestamp = timestamp;
                return;
            }

            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_lastRenderingTimestamp, timestamp).TotalMilliseconds;
            var millisecondsSinceLastInput =
                System.Diagnostics.Stopwatch.GetElapsedTime(_lastInputTimestamp, timestamp).TotalMilliseconds;
            _lastRenderingTimestamp = timestamp;
            var current = viewer.VerticalOffset;
            var next = InterpolateOffset(current, _targetOffset, elapsed);
            if (ShouldSnapToTarget(Math.Abs(_targetOffset - next), millisecondsSinceLastInput))
            {
                next = _targetOffset;
            }

            viewer.ScrollToVerticalOffset(next);
            if (next == _targetOffset)
            {
                StopAnimation();
            }
        }

        private void StopAnimation()
        {
            if (_isAnimating)
            {
                CompositionTarget.Rendering -= CompositionTargetRendering;
            }

            _isAnimating = false;
            _lastRenderingTimestamp = 0;
            _lastInputTimestamp = 0;
        }

        private static T? FindVisualDescendant<T>(DependencyObject root)
            where T : DependencyObject
        {
            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    return match;
                }

                var nested = FindVisualDescendant<T>(child);
                if (nested is not null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
