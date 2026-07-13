using System.Windows;

namespace TileStart.Host;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void ShowFromShell()
    {
        Left = SystemParameters.WorkArea.Right - Width;
        Top = SystemParameters.WorkArea.Bottom - Height;

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Focus();
    }
}
