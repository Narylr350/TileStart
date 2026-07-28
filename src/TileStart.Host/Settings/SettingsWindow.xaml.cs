using System.IO;
using System.Windows;
using System.Windows.Input;
using TileStart.Host.Shell;
using TileStart.Host.Themes;
using TileStart.Host.Utilities;
using TileStart.Host.Windowing;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace TileStart.Host.Settings;

public partial class SettingsWindow : Window
{
    private readonly Action<Window> _openBackupAndRestore;
    private readonly Action<Window> _openAbout;

    public AppThemeStyle SelectedThemeStyle { get; private set; }
    public AppColorMode SelectedColorMode { get; private set; }

    public SettingsWindow(
        AppThemeStyle currentThemeStyle,
        AppColorMode currentColorMode,
        Action<Window> openBackupAndRestore,
        Action<Window> openAbout)
    {
        InitializeComponent();
        SelectedThemeStyle = currentThemeStyle;
        SelectedColorMode = currentColorMode;
        _openBackupAndRestore = openBackupAndRestore;
        _openAbout = openAbout;
        StartupBox.IsChecked = StartupRegistration.IsEnabled();
        Windows10Choice.IsChecked = currentThemeStyle == AppThemeStyle.Windows10;
        Windows11Choice.IsChecked = currentThemeStyle == AppThemeStyle.Windows11;
        SystemColorChoice.IsChecked = currentColorMode == AppColorMode.System;
        LightColorChoice.IsChecked = currentColorMode == AppColorMode.Light;
        DarkColorChoice.IsChecked = currentColorMode == AppColorMode.Dark;
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

        SelectedThemeStyle = Windows10Choice.IsChecked == true
            ? AppThemeStyle.Windows10
            : AppThemeStyle.Windows11;
        SelectedColorMode = LightColorChoice.IsChecked == true
            ? AppColorMode.Light
            : DarkColorChoice.IsChecked == true
                ? AppColorMode.Dark
                : AppColorMode.System;
        DialogResult = true;
    }

    private void BackupRestore_Click(object sender, RoutedEventArgs e)
    {
        _openBackupAndRestore(this);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        _openAbout(this);
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 TileStart 诊断包",
            Filter = "ZIP 压缩包 (*.zip)|*.zip",
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = $"TileStart-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            DiagnosticBundleService.Export(dialog.FileName);
            TileStartMessageDialog.Show(
                this,
                "诊断包已导出",
                "诊断包已导出。公开提交前请先检查日志中可能包含的本地路径和应用名称。",
                TileStartMessageKind.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TileStartMessageDialog.Show(
                this,
                "无法导出诊断包",
                exception.Message,
                TileStartMessageKind.Error);
        }
    }
}