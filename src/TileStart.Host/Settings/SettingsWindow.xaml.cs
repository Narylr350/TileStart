using System.Windows;
using System.Windows.Input;
using TileStart.Host.Shell;
using TileStart.Host.Themes;

namespace TileStart.Host.Settings;

public partial class SettingsWindow : Window
{
    private readonly AppThemeStyle _currentThemeStyle;
    private readonly Action<AppThemeStyle> _changeThemeStyle;
    private readonly Func<Task> _checkForUpdates;
    private readonly Action _openBackupAndRestore;
    private readonly Action _openAbout;

    public SettingsWindow(
        AppThemeStyle currentThemeStyle,
        Action<AppThemeStyle> changeThemeStyle,
        Func<Task> checkForUpdates,
        Action openBackupAndRestore,
        Action openAbout)
    {
        InitializeComponent();
        _currentThemeStyle = currentThemeStyle;
        _changeThemeStyle = changeThemeStyle;
        _checkForUpdates = checkForUpdates;
        _openBackupAndRestore = openBackupAndRestore;
        _openAbout = openAbout;
        StartupBox.IsChecked = StartupRegistration.IsEnabled();
        Windows10Choice.IsChecked = currentThemeStyle == AppThemeStyle.Windows10;
        Windows11Choice.IsChecked = currentThemeStyle == AppThemeStyle.Windows11;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var startupEnabled = StartupBox.IsChecked == true;
        if (StartupRegistration.IsEnabled() != startupEnabled && !StartupRegistration.SetEnabled(startupEnabled))
        {
            StatusText.Text = "无法修改登录启动设置。";
            return;
        }

        var selectedTheme = Windows10Choice.IsChecked == true
            ? AppThemeStyle.Windows10
            : AppThemeStyle.Windows11;
        if (selectedTheme != _currentThemeStyle)
        {
            Close();
            _changeThemeStyle(selectedTheme);
            return;
        }

        DialogResult = true;
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        Close();
        await _checkForUpdates();
    }

    private void BackupRestore_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _openBackupAndRestore();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _openAbout();
    }
}