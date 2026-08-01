using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseEventHandler = System.Windows.Input.MouseEventHandler;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace TileStart.Host;

public static class Win10InteractionMotion
{
    public const double PressedScale = 0.975;
    // StartUI 按 CellSize 分别保存 X/Y 缩放，目标是每个方向约收缩 5 DIP；
    // 宽磁贴因此必须横向 0.9755、纵向 0.95，不能重新合并成统一比例。
    public const double SmallTilePressedScale = 0.8958;
    public const double MediumTilePressedScale = 0.95;
    public const double WideTilePressedScale = 0.9755;
    // UpdateTileScaling 对拖动源使用另一组尺寸常量：小磁贴放大 8%，中/宽磁贴放大 4%，
    // 大磁贴保持原尺寸；这些状态不能简化为统一的拖动比例。
    public const double SmallTileDragScale = 1.08;
    public const double StandardTileDragScale = 1.04;
    public const double LargeTileDragScale = 1;
    public const int PressTransitionDurationMilliseconds = 167;

    public static readonly Point PressSplineControlPoint1 = new(0.1, 0.9);
    public static readonly Point PressSplineControlPoint2 = new(0.2, 1);

    public static Size TilePressedScale(Size bounds) =>
        new(TilePressedScaleForLength(bounds.Width), TilePressedScaleForLength(bounds.Height));

    public static double TileDragScale(Size bounds)
    {
        if (bounds.Width <= Win10TileMetrics.CellSize && bounds.Height <= Win10TileMetrics.CellSize)
        {
            return SmallTileDragScale;
        }

        if (bounds.Width > Win10TileMetrics.WidthForColumns(2)
            && bounds.Height > Win10TileMetrics.HeightForRows(2))
        {
            return LargeTileDragScale;
        }

        return StandardTileDragScale;
    }

    public static DoubleAnimationUsingKeyFrames CreateScaleAnimation(double from, double to)
    {
        var duration = TimeSpan.FromMilliseconds(PressTransitionDurationMilliseconds);
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            FillBehavior = FillBehavior.Stop,
        };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(
            to,
            KeyTime.FromTimeSpan(duration),
            new KeySpline(PressSplineControlPoint1, PressSplineControlPoint2)));
        return animation;
    }

    private static double TilePressedScaleForLength(double length)
    {
        if (length <= 0)
        {
            return 1;
        }

        if (length <= Win10TileMetrics.CellSize)
        {
            return SmallTilePressedScale;
        }

        if (length <= Win10TileMetrics.WidthForColumns(2))
        {
            return MediumTilePressedScale;
        }

        return WideTilePressedScale;
    }

    public static bool IsPointerWithinRevealRadius(Point pointer, Size bounds, double radius)
    {
        var nearestX = Math.Clamp(pointer.X, 0, bounds.Width);
        var nearestY = Math.Clamp(pointer.Y, 0, bounds.Height);
        var deltaX = pointer.X - nearestX;
        var deltaY = pointer.Y - nearestY;
        var constrainedRadius = Math.Max(1, radius);
        return (deltaX * deltaX) + (deltaY * deltaY) <= constrainedRadius * constrainedRadius;
    }

    public static double ConstrainCornerRadius(CornerRadius cornerRadius, Size bounds)
    {
        var requested = Math.Max(
            Math.Max(cornerRadius.TopLeft, cornerRadius.TopRight),
            Math.Max(cornerRadius.BottomRight, cornerRadius.BottomLeft));
        var maximum = Math.Max(0, Math.Min(bounds.Width, bounds.Height) / 2);
        return Math.Clamp(requested, 0, maximum);
    }
}

public sealed class Win10InteractionBorder : Border
{
    public static readonly DependencyProperty IsPressedStateProperty = DependencyProperty.Register(
        nameof(IsPressedState),
        typeof(bool),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(false, OnIsPressedStateChanged));

    public static readonly DependencyProperty IsRevealEnabledProperty = DependencyProperty.Register(
        nameof(IsRevealEnabled),
        typeof(bool),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPressAnimationEnabledProperty = DependencyProperty.Register(
        nameof(IsPressAnimationEnabled),
        typeof(bool),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty UsesTilePressedScaleProperty = DependencyProperty.Register(
        nameof(UsesTilePressedScale),
        typeof(bool),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty UsesSharedPointerLightProperty = DependencyProperty.Register(
        nameof(UsesSharedPointerLight),
        typeof(bool),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(false, OnUsesSharedPointerLightChanged));

    public static readonly DependencyProperty RevealRadiusProperty = DependencyProperty.Register(
        nameof(RevealRadius),
        typeof(double),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(96d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoverFillOpacityProperty = DependencyProperty.Register(
        nameof(HoverFillOpacity),
        typeof(double),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(0.14d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PressedFillOpacityProperty = DependencyProperty.Register(
        nameof(PressedFillOpacity),
        typeof(double),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(0.20d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RevealBorderOpacityProperty = DependencyProperty.Register(
        nameof(RevealBorderOpacity),
        typeof(double),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(0.50d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RevealBorderThicknessProperty = DependencyProperty.Register(
        nameof(RevealBorderThickness),
        typeof(double),
        typeof(Win10InteractionBorder),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly ScaleTransform _pressScale = new(1, 1);
    private Point _pointerPosition;
    private bool _hasPointerLight;

    public Win10InteractionBorder()
    {
        ClipToBounds = true;
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _pressScale;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool IsPressedState
    {
        get => (bool)GetValue(IsPressedStateProperty);
        set => SetValue(IsPressedStateProperty, value);
    }

    public bool IsRevealEnabled
    {
        get => (bool)GetValue(IsRevealEnabledProperty);
        set => SetValue(IsRevealEnabledProperty, value);
    }

    public bool IsPressAnimationEnabled
    {
        get => (bool)GetValue(IsPressAnimationEnabledProperty);
        set => SetValue(IsPressAnimationEnabledProperty, value);
    }

    public bool UsesTilePressedScale
    {
        get => (bool)GetValue(UsesTilePressedScaleProperty);
        set => SetValue(UsesTilePressedScaleProperty, value);
    }

    public bool UsesSharedPointerLight
    {
        get => (bool)GetValue(UsesSharedPointerLightProperty);
        set => SetValue(UsesSharedPointerLightProperty, value);
    }

    public double RevealRadius
    {
        get => (double)GetValue(RevealRadiusProperty);
        set => SetValue(RevealRadiusProperty, value);
    }

    public double HoverFillOpacity
    {
        get => (double)GetValue(HoverFillOpacityProperty);
        set => SetValue(HoverFillOpacityProperty, value);
    }

    public double PressedFillOpacity
    {
        get => (double)GetValue(PressedFillOpacityProperty);
        set => SetValue(PressedFillOpacityProperty, value);
    }

    public double RevealBorderOpacity
    {
        get => (double)GetValue(RevealBorderOpacityProperty);
        set => SetValue(RevealBorderOpacityProperty, value);
    }

    public double RevealBorderThickness
    {
        get => (double)GetValue(RevealBorderThicknessProperty);
        set => SetValue(RevealBorderThicknessProperty, value);
    }

    internal void UpdatePointerLight(Point position, bool isActive)
    {
        _pointerPosition = position;
        _hasPointerLight = isActive;
        InvalidateVisual();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (!UsesSharedPointerLight)
        {
            UpdatePointerLight(e.GetPosition(this), true);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!UsesSharedPointerLight)
        {
            UpdatePointerLight(e.GetPosition(this), true);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!UsesSharedPointerLight)
        {
            UpdatePointerLight(default, false);
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var hasNearbyLight = _hasPointerLight
                             && Win10InteractionMotion.IsPointerWithinRevealRadius(
                                 _pointerPosition,
                                 new Size(ActualWidth, ActualHeight),
                                 RevealRadius);
        if (!IsRevealEnabled
            || (!hasNearbyLight && !IsPressedState)
            || ActualWidth <= 0
            || ActualHeight <= 0)
        {
            return;
        }

        var pointer = _hasPointerLight
            ? _pointerPosition
            : new Point(ActualWidth / 2, ActualHeight / 2);
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        var cornerRadius = Win10InteractionMotion.ConstrainCornerRadius(
            CornerRadius,
            new Size(ActualWidth, ActualHeight));
        var fillOpacity = IsPressedState ? PressedFillOpacity : HoverFillOpacity;
        if (fillOpacity > 0)
        {
            var fill = CreateRevealBrush(pointer, fillOpacity);
            drawingContext.DrawRoundedRectangle(fill, null, bounds, cornerRadius, cornerRadius);
        }

        var borderThickness = Math.Max(0, RevealBorderThickness);
        if (RevealBorderOpacity <= 0
            || borderThickness <= 0
            || ActualWidth <= borderThickness
            || ActualHeight <= borderThickness)
        {
            return;
        }

        var border = CreateRevealBrush(pointer, RevealBorderOpacity);
        var pen = new Pen(border, borderThickness);
        var borderInset = borderThickness / 2;
        var borderCornerRadius = Math.Max(0, cornerRadius - borderInset);
        drawingContext.DrawRoundedRectangle(
            null,
            pen,
            new Rect(
                borderInset,
                borderInset,
                ActualWidth - borderThickness,
                ActualHeight - borderThickness),
            borderCornerRadius,
            borderCornerRadius);
    }

    private static void OnIsPressedStateChanged(DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var border = (Win10InteractionBorder)dependencyObject;
        var target = (bool)args.NewValue
            ? border.UsesTilePressedScale
                ? Win10InteractionMotion.TilePressedScale(new Size(border.ActualWidth, border.ActualHeight))
                : new Size(Win10InteractionMotion.PressedScale, Win10InteractionMotion.PressedScale)
            : new Size(1, 1);
        border.AnimatePressScale(target);
        border.InvalidateVisual();
    }

    private static void OnUsesSharedPointerLightChanged(DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var border = (Win10InteractionBorder)dependencyObject;
        if (!border.IsLoaded)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            Win10SharedPointerLight.Register(border);
        }
        else
        {
            Win10SharedPointerLight.Unregister(border);
            border.UpdatePointerLight(default, false);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (UsesSharedPointerLight)
        {
            Win10SharedPointerLight.Register(this);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Win10SharedPointerLight.Unregister(this);

    private void AnimatePressScale(Size target)
    {
        var currentX = _pressScale.ScaleX;
        var currentY = _pressScale.ScaleY;
        _pressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _pressScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _pressScale.ScaleX = target.Width;
        _pressScale.ScaleY = target.Height;

        if (!IsPressAnimationEnabled || !SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        _pressScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            Win10InteractionMotion.CreateScaleAnimation(currentX, target.Width),
            HandoffBehavior.SnapshotAndReplace);
        _pressScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            Win10InteractionMotion.CreateScaleAnimation(currentY, target.Height),
            HandoffBehavior.SnapshotAndReplace);
    }

    private RadialGradientBrush CreateRevealBrush(Point pointer, double opacity)
    {
        var radius = Math.Max(1, RevealRadius);
        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = pointer,
            GradientOrigin = pointer,
            RadiusX = radius,
            RadiusY = radius,
        };
        brush.GradientStops.Add(new GradientStop(White(opacity), 0));
        brush.GradientStops.Add(new GradientStop(White(opacity * 0.48), 0.45));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        return brush;
    }

    private static Color White(double opacity) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0, 1) * byte.MaxValue), byte.MaxValue, byte.MaxValue,
            byte.MaxValue);
}

internal static class Win10SharedPointerLight
{
    private static readonly Dictionary<Win10InteractionBorder, WindowPointerLight> Registrations = [];
    private static readonly Dictionary<Window, WindowPointerLight> Windows = [];

    public static void Register(Win10InteractionBorder border)
    {
        if (Registrations.ContainsKey(border))
        {
            return;
        }

        var window = Window.GetWindow(border);
        if (window is null)
        {
            return;
        }

        if (!Windows.TryGetValue(window, out var pointerLight))
        {
            pointerLight = new WindowPointerLight(window, borders =>
            {
                Windows.Remove(window);
                foreach (var registeredBorder in borders)
                {
                    Registrations.Remove(registeredBorder);
                }
            });
            Windows.Add(window, pointerLight);
        }

        Registrations.Add(border, pointerLight);
        pointerLight.Register(border);
    }

    public static void Unregister(Win10InteractionBorder border)
    {
        if (Registrations.Remove(border, out var pointerLight))
        {
            pointerLight.Unregister(border);
        }
    }

    private sealed class WindowPointerLight
    {
        private readonly HashSet<Win10InteractionBorder> _borders = [];
        private readonly Action<IReadOnlyCollection<Win10InteractionBorder>> _closed;
        private readonly MouseEventHandler _mouseMoveHandler;
        private readonly Window _window;
        private Point _pointerPosition;
        private bool _isActive;

        public WindowPointerLight(Window window, Action<IReadOnlyCollection<Win10InteractionBorder>> closed)
        {
            _window = window;
            _closed = closed;
            _mouseMoveHandler = OnMouseMove;
            _window.AddHandler(Mouse.MouseMoveEvent, _mouseMoveHandler, true);
            _window.MouseLeave += OnMouseLeave;
            _window.Deactivated += OnDeactivated;
            _window.Closed += OnClosed;
        }

        public void Register(Win10InteractionBorder border)
        {
            _borders.Add(border);
            Publish(border);
        }

        public void Unregister(Win10InteractionBorder border)
        {
            _borders.Remove(border);
            border.UpdatePointerLight(default, false);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _pointerPosition = e.GetPosition(_window);
            _isActive = true;
            Publish();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isActive = false;
            Publish();
        }

        private void OnDeactivated(object? sender, EventArgs e)
        {
            _isActive = false;
            Publish();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _window.RemoveHandler(Mouse.MouseMoveEvent, _mouseMoveHandler);
            _window.MouseLeave -= OnMouseLeave;
            _window.Deactivated -= OnDeactivated;
            _window.Closed -= OnClosed;
            var borders = _borders.ToArray();
            foreach (var border in borders)
            {
                border.UpdatePointerLight(default, false);
            }

            _borders.Clear();
            _closed(borders);
        }

        private void Publish()
        {
            foreach (var border in _borders.ToArray())
            {
                Publish(border);
            }
        }

        private void Publish(Win10InteractionBorder border)
        {
            if (!_isActive || !border.IsLoaded || !border.IsVisible || !border.IsEnabled)
            {
                border.UpdatePointerLight(default, false);
                return;
            }

            try
            {
                border.UpdatePointerLight(_window.TranslatePoint(_pointerPosition, border), true);
            }
            catch (InvalidOperationException)
            {
                border.UpdatePointerLight(default, false);
            }
        }
    }
}