using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace TileStart.Host.Windowing;

public enum TileStartMessageKind
{
    Information,
    Warning,
    Error,
    Question,
}

public partial class TileStartMessageDialog : Window
{
    private TileStartMessageDialog(
        Window? owner,
        string title,
        string message,
        TileStartMessageKind kind,
        string primaryText,
        string? secondaryText)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;
        SecondaryButton.Content = secondaryText;
        SecondaryButton.Visibility = secondaryText is null ? Visibility.Collapsed : Visibility.Visible;
        ApplyKind(kind);

        owner ??= ResolveActiveOwner();
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            Owner = owner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public static void Show(
        Window? owner,
        string title,
        string message,
        TileStartMessageKind kind = TileStartMessageKind.Information,
        string primaryText = "确定")
    {
        _ = new TileStartMessageDialog(owner, title, message, kind, primaryText, secondaryText: null).ShowDialog();
    }

    public static bool Confirm(
        Window? owner,
        string title,
        string message,
        TileStartMessageKind kind = TileStartMessageKind.Question,
        string primaryText = "继续",
        string secondaryText = "取消")
    {
        return new TileStartMessageDialog(owner, title, message, kind, primaryText, secondaryText).ShowDialog() == true;
    }

    private static Window? ResolveActiveOwner()
    {
        if (WpfApplication.Current is not { } application)
        {
            return null;
        }

        return application.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
               ?? (application.MainWindow?.IsVisible == true ? application.MainWindow : null);
    }

    private void ApplyKind(TileStartMessageKind kind)
    {
        KindIcon.Text = kind switch
        {
            TileStartMessageKind.Warning => "\uE7BA",
            TileStartMessageKind.Error => "\uEA39",
            TileStartMessageKind.Question => "\uE897",
            _ => "\uE946",
        };
        KindIcon.Foreground = kind switch
        {
            TileStartMessageKind.Warning => FrozenBrush(MediaColor.FromRgb(0xF5, 0xA6, 0x23)),
            TileStartMessageKind.Error => FrozenBrush(MediaColor.FromRgb(0xE5, 0x48, 0x4D)),
            _ => Win10Theme.AccentBrush,
        };
    }

    private static SolidColorBrush FrozenBrush(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void SecondaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
