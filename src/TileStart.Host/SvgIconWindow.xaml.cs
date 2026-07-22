using System.Windows;
using System.Windows.Input;

namespace TileStart.Host;

public partial class SvgIconWindow : Window
{
    public SvgIconWindow(string source = "")
    {
        InitializeComponent();
        SourceBox.Text = source;
        if (!string.IsNullOrWhiteSpace(source))
        {
            UpdatePreview();
        }
    }

    public string IconPath { get; private set; } = string.Empty;
    public string Source => SourceBox.Text.Trim();

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        try
        {
            CustomIconStore.ValidateSvg(Source);
            PreviewImage.Source = SvgIconLoader.LoadText(Source)
                                  ?? throw new InvalidOperationException("SVG 无法渲染，请检查代码内容。");
            ConfirmButton.IsEnabled = true;
            StatusText.Text = "SVG 有效，可以使用。";
        }
        catch (Exception exception)
        {
            PreviewImage.Source = null;
            ConfirmButton.IsEnabled = false;
            StatusText.Text = exception.Message;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IconPath = CustomIconStore.SaveSvg(Source);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ConfirmButton.IsEnabled = false;
            StatusText.Text = exception.Message;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
