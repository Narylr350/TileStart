using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using TileStart.Host;
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
        NameTextBlock.Opacity = isPlaceholder ? 0.72 : 1;
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

        if (_isEditing)
        {
            var accent = SystemParameters.WindowGlassColor;
            InteractionBorder.Background = CreateBrush(0x38, accent.R, accent.G, accent.B);
            InteractionBorder.BorderBrush = CreateBrush(0xd0, accent.R, accent.G, accent.B);
        }
        else if (_isDragging || _isPressed)
        {
            var accent = SystemParameters.WindowGlassColor;
            InteractionBorder.Background = CreateBrush(0x38, accent.R, accent.G, accent.B);
            InteractionBorder.BorderBrush = TransparentBrush;
        }
        else
        {
            InteractionBorder.Background = TransparentBrush;
            InteractionBorder.BorderBrush = TransparentBrush;
        }

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

    private static Brush CreateBrush(byte alpha, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }
}