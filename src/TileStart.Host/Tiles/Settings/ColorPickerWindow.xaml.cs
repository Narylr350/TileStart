using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace TileStart.Host.Tiles.Settings;

public partial class ColorPickerWindow : Window
{
    private static readonly string[] SwatchHexValues =
    [
        "#000000", "#3A3A3A", "#8A8A8A", "#FFFFFF",
        "#C42B1C", "#F7630C", "#FFB900", "#107C10",
        "#0078D4", "#4CC2FF", "#8764B8", "#E3008C",
    ];

    private ColorPickerMath.Hsv _hsv;
    private bool _suppressSync;

    public ColorPickerWindow(string initialColor = "")
    {
        InitializeComponent();
        SwatchList.ItemsSource = SwatchHexValues
            .Select(hex => new { Hex = hex, Brush = CreateFrozenBrush(hex) })
            .ToArray();

        SelectedColor = ColorPickerMath.TryParseHex(initialColor, out var parsed)
            ? parsed
            : MediaColor.FromRgb(0x3A, 0x3A, 0x3A);
        _hsv = ColorPickerMath.ToHsv(SelectedColor);
        Loaded += (_, _) => SyncFromHsv();
    }

    /// <summary>Colour chosen when the dialog is confirmed.</summary>
    public MediaColor SelectedColor { get; private set; }

    /// <summary>Chosen colour as <c>#RRGGBB</c>.</summary>
    public string SelectedHex => ColorPickerMath.ToHex(SelectedColor);

    private static SolidColorBrush CreateFrozenBrush(string hex)
    {
        ColorPickerMath.TryParseHex(hex, out var color);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>Pushes the current HSV state into the spectrum, inputs and preview.</summary>
    private void SyncFromHsv()
    {
        SelectedColor = ColorPickerMath.ToColor(_hsv);
        SpectrumHueStop.Color = ColorPickerMath.HueColor(_hsv.Hue);
        PreviewSwatch.Background = new SolidColorBrush(SelectedColor);

        _suppressSync = true;
        HexBox.Text = SelectedHex;
        RedBox.Text = SelectedColor.R.ToString();
        GreenBox.Text = SelectedColor.G.ToString();
        BlueBox.Text = SelectedColor.B.ToString();
        _suppressSync = false;

        PositionThumbs();
    }

    private void PositionThumbs()
    {
        var spectrumWidth = SpectrumHost.ActualWidth - SpectrumHost.BorderThickness.Left * 2;
        var spectrumHeight = SpectrumHost.ActualHeight - SpectrumHost.BorderThickness.Top * 2;
        if (spectrumWidth > 0 && spectrumHeight > 0)
        {
            Canvas.SetLeft(SpectrumThumb, (_hsv.Saturation * spectrumWidth) - (SpectrumThumb.Width / 2));
            Canvas.SetTop(SpectrumThumb, ((1 - _hsv.Value) * spectrumHeight) - (SpectrumThumb.Height / 2));
        }

        var hueWidth = HueHost.ActualWidth - HueHost.BorderThickness.Left * 2;
        if (hueWidth > 0)
        {
            Canvas.SetLeft(HueThumb, (_hsv.Hue / 360 * hueWidth) - (HueThumb.Width / 2));
        }
    }

    private void UpdateFromColor(MediaColor color)
    {
        SelectedColor = color;
        var hsv = ColorPickerMath.ToHsv(color);
        // Greys carry no hue; keep the slider where the user left it so the spectrum
        // does not jump back to red when they drag value down to black.
        _hsv = hsv.Saturation == 0
            ? new ColorPickerMath.Hsv(_hsv.Hue, hsv.Saturation, hsv.Value)
            : hsv;
        SyncFromHsv();
    }

    private void TrackSpectrum(MouseEventArgs e)
    {
        var position = e.GetPosition(SpectrumHost);
        var width = SpectrumHost.ActualWidth - SpectrumHost.BorderThickness.Left * 2;
        var height = SpectrumHost.ActualHeight - SpectrumHost.BorderThickness.Top * 2;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _hsv = new ColorPickerMath.Hsv(
            _hsv.Hue,
            Math.Clamp(position.X / width, 0, 1),
            Math.Clamp(1 - (position.Y / height), 0, 1));
        SyncFromHsv();
    }

    private void TrackHue(MouseEventArgs e)
    {
        var width = HueHost.ActualWidth - HueHost.BorderThickness.Left * 2;
        if (width <= 0)
        {
            return;
        }

        var position = e.GetPosition(HueHost);
        _hsv = new ColorPickerMath.Hsv(
            Math.Clamp(position.X / width, 0, 1) * 360,
            _hsv.Saturation,
            _hsv.Value);
        SyncFromHsv();
    }

    private void Spectrum_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SpectrumHost.CaptureMouse();
        TrackSpectrum(e);
    }

    private void Spectrum_MouseMove(object sender, MouseEventArgs e)
    {
        if (SpectrumHost.IsMouseCaptured)
        {
            TrackSpectrum(e);
        }
    }

    private void Spectrum_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SpectrumHost.ReleaseMouseCapture();
    }

    private void Hue_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HueHost.CaptureMouse();
        TrackHue(e);
    }

    private void Hue_MouseMove(object sender, MouseEventArgs e)
    {
        if (HueHost.IsMouseCaptured)
        {
            TrackHue(e);
        }
    }

    private void Hue_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        HueHost.ReleaseMouseCapture();
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSync || !ColorPickerMath.TryParseHex(HexBox.Text, out var color))
        {
            return;
        }

        UpdateFromColor(color);
    }

    private void ChannelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSync
            || !TryReadChannel(RedBox, out var red)
            || !TryReadChannel(GreenBox, out var green)
            || !TryReadChannel(BlueBox, out var blue))
        {
            return;
        }

        UpdateFromColor(MediaColor.FromRgb(red, green, blue));
    }

    private static bool TryReadChannel(TextBox box, out byte value) =>
        byte.TryParse(box.Text, out value);

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string hex }
            && ColorPickerMath.TryParseHex(hex, out var color))
        {
            UpdateFromColor(color);
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
