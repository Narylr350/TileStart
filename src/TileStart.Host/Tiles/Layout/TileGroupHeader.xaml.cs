using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TileStart.Host.Themes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using TileStart.Host.Tiles.Models;

namespace TileStart.Host.Tiles.Layout;

public partial class TileGroupHeader : System.Windows.Controls.UserControl
{
    private static readonly Brush TransparentBrush = CreateBrush(0, 0, 0, 0);
    private TileGroup? _group;
    private string _originalName = string.Empty;
    private bool _isEditing;
    private bool _isDragging;
    private bool _isPressed;
    private bool _isPointerOver;
    private bool _endingEdit;

    public TileGroupHeader()
    {
        InitializeComponent();
        DataContextChanged += TileGroupHeader_DataContextChanged;
    }

    public bool IsEditing => _isEditing;

    public event EventHandler? NameCommitted;

    public void BeginEdit()
    {
        if (_group is null || _isEditing || _isDragging)
        {
            return;
        }

        _originalName = _group.Name;
        _isEditing = true;
        NameTextBox.Text = _originalName;
        NameTextBlockHost.Visibility = Visibility.Collapsed;
        NameTextBox.Visibility = Visibility.Visible;
        NameTextBoxHost.IsHitTestVisible = true;
        ApplyVisualState();

        Dispatcher.BeginInvoke(() =>
        {
            NameTextBox.Focus();
            NameTextBox.CaretIndex = NameTextBox.Text.Length;
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    public void SetDragging(bool value)
    {
        _isDragging = value;
        _isPressed = value;
        ApplyVisualState();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        _isPointerOver = true;
        ApplyVisualState();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _isPointerOver = false;
        ApplyVisualState();
        base.OnMouseLeave(e);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!_isEditing)
        {
            _isPressed = true;
            ApplyVisualState();
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            _isPressed = false;
            ApplyVisualState();
        }

        base.OnPreviewMouseLeftButtonUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (!_isDragging)
        {
            _isPressed = false;
            ApplyVisualState();
        }

        base.OnLostMouseCapture(e);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        ApplyVisualState();
        base.OnGotKeyboardFocus(e);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        ApplyVisualState();
        base.OnLostKeyboardFocus(e);
    }

    private void TileGroupHeader_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_group is not null)
        {
            _group.PropertyChanged -= Group_PropertyChanged;
        }

        _group = e.NewValue as TileGroup;
        if (_group is not null)
        {
            _group.PropertyChanged += Group_PropertyChanged;
        }

        UpdateText();
    }

    private void Group_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TileGroup.Name) && !_isEditing)
        {
            UpdateText();
        }
    }

    private void NameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndEdit(commit: false);
            e.Handled = true;
        }
    }

    private void NameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_endingEdit)
        {
            CommitEdit();
        }
    }

    private void CommitEdit()
    {
        if (_group is null || !_isEditing)
        {
            return;
        }

        var changed = _group.Name != NameTextBox.Text;
        _group.Name = NameTextBox.Text;
        EndEdit(commit: true);
        if (changed)
        {
            NameCommitted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EndEdit(bool commit)
    {
        if (!_isEditing)
        {
            return;
        }

        _endingEdit = true;
        if (!commit && _group is not null)
        {
            NameTextBox.Text = _originalName;
        }

        _isEditing = false;
        NameTextBoxHost.IsHitTestVisible = false;
        NameTextBox.Visibility = Visibility.Collapsed;
        Keyboard.ClearFocus();
        UpdateText();
        ApplyVisualState();
        _endingEdit = false;
    }

    private void UpdateText()
    {
        var name = _group?.Name;
        var isPlaceholder = string.IsNullOrWhiteSpace(name);
        NameTextBlock.Text = isPlaceholder ? "命名组" : name;
        // TileGroupHeaderPlaceholderTextHoverBrush resolves to SystemBaseMediumColor.
        NameTextBlock.Opacity = isPlaceholder ? 0.60 : 1;
        UpdateTitleVisibility();
    }

    private void ApplyVisualState()
    {
        var interactive = _isEditing || _isDragging || _isPressed || _isPointerOver;
        NameTextBlock.Margin = interactive
            ? Win10VisualMetrics.TileGroupTitleInteractiveMargin
            : Win10VisualMetrics.TileGroupTitleRestMargin;
        Gripper.Visibility = interactive ? Visibility.Visible : Visibility.Collapsed;
        UpdateTitleVisibility();

        var interactionVisual = ResolveInteractionVisual(
            AppThemeManager.CurrentStyle,
            _isEditing,
            _isDragging,
            _isPressed);
        var accent = Win10Theme.AccentColor;
        InteractionBorder.BorderThickness = new Thickness(interactionVisual.BorderThickness);
        InteractionBorder.Background = interactionVisual.BackgroundAlpha == 0
            ? TransparentBrush
            : CreateBrush(interactionVisual.BackgroundAlpha, accent.R, accent.G, accent.B);
        InteractionBorder.BorderBrush = interactionVisual.BorderAlpha == 0
            ? TransparentBrush
            : CreateBrush(interactionVisual.BorderAlpha, accent.R, accent.G, accent.B);

        var keyboardFocus = IsKeyboardFocusWithin && !_isEditing && !_isPressed && !_isDragging;
        PrimaryFocusVisual.Visibility = keyboardFocus ? Visibility.Visible : Visibility.Collapsed;
        SecondaryFocusVisual.Visibility = keyboardFocus ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateTitleVisibility()
    {
        var isPlaceholder = string.IsNullOrWhiteSpace(_group?.Name);
        var showPlaceholder = _isPointerOver || _isPressed || _isDragging || IsKeyboardFocusWithin;
        NameTextBlockHost.Visibility = !_isEditing && (!isPlaceholder || showPlaceholder)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    internal static (byte BackgroundAlpha, byte BorderAlpha, double BorderThickness) ResolveInteractionVisual(
        AppThemeStyle style,
        bool isEditing,
        bool isDragging,
        bool isPressed)
    {
        if (style == AppThemeStyle.Windows10)
        {
            // StartUI 的 AccentHighlightBrush 是完整 SystemAccentColor；Drag 只通过控件 Opacity=0.8 降低强度。
            // WPF 这里将该整体透明度映射到背景 alpha，同时保持原版 Drag 的零描边，避免改变标题内容透明度。
            if (isDragging)
            {
                // 透明 BorderBrush 仍会让 WPF Border 把 Background 向内挤；原版 Drag 必须是真正的零描边。
                return (0xcc, 0, 0);
            }

            if (isEditing || isPressed)
            {
                return (0xff, 0xff, Win10VisualMetrics.TileGroupHeaderStrokeThickness);
            }

            return (0, 0, Win10VisualMetrics.TileGroupHeaderStrokeThickness);
        }

        if (isEditing)
        {
            return (0x38, 0xd0, Win10VisualMetrics.TileGroupHeaderStrokeThickness);
        }

        return isDragging || isPressed
            ? ((byte)0x38, (byte)0, Win10VisualMetrics.TileGroupHeaderStrokeThickness)
            : ((byte)0, (byte)0, Win10VisualMetrics.TileGroupHeaderStrokeThickness);
    }

    private static Brush CreateBrush(byte alpha, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }
}