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
    private readonly Action<AppThemeStyle, AppColorMode> _changeAppearance;
    private readonly bool _initialStartupEnabled;

    public AppThemeStyle SelectedThemeStyle { get; private set; }
    public AppColorMode SelectedColorMode { get; private set; }
    public bool SelectedStartupEnabled { get; private set; }
    public bool StartupChanged => SelectedStartupEnabled != _initialStartupEnabled;
    public bool WasSaved { get; private set; }

    public SettingsWindow(
        AppThemeStyle currentThemeStyle,
        AppColorMode currentColorMode,
        Action<Window> openBackupAndRestore,
        Action<Window> openAbout,
        Action<AppThemeStyle, AppColorMode> changeAppearance)
    {
        InitializeComponent();
        SelectedThemeStyle = currentThemeStyle;
        SelectedColorMode = currentColorMode;
        _openBackupAndRestore = openBackupAndRestore;
        _openAbout = openAbout;
        _changeAppearance = changeAppearance;
        _initialStartupEnabled = StartupRegistration.IsEnabled();
        SelectedStartupEnabled = _initialStartupEnabled;
        StartupBox.IsChecked = _initialStartupEnabled;
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
        SelectedStartupEnabled = StartupBox.IsChecked == true;
        SelectedThemeStyle = ResolveSelectedThemeStyle(
            Windows10Choice.IsChecked,
            Windows11Choice.IsChecked,
            SelectedThemeStyle);
        DiagnosticLog.Write($"Settings save requested: win10={Windows10Choice.IsChecked}, win11={Windows11Choice.IsChecked}, selected={SelectedThemeStyle}.");
        SelectedColorMode = LightColorChoice.IsChecked == true
            ? AppColorMode.Light
            : DarkColorChoice.IsChecked == true
                ? AppColorMode.Dark
                : AppColorMode.System;

        // 外观保存不能依赖 ShowDialog 的返回值：Win10 前台监控可能隐藏 owner，令模态窗口返回 null。
        // 先直接落盘并安排重启，再把关闭结果和启动项选择交回 App。
        WasSaved = true;
        _changeAppearance(SelectedThemeStyle, SelectedColorMode);
        DialogResult = true;
    }

    private void ThemeChoice_Checked(object sender, RoutedEventArgs e)
    {
        // 自定义模板在部分 Win10 环境中可能短暂保留两个选中状态；显式互斥保证保存值跟随最后点击项。
        if (ReferenceEquals(sender, Windows11Choice))
        {
            Windows10Choice.IsChecked = false;
        }
        else if (ReferenceEquals(sender, Windows10Choice))
        {
            Windows11Choice.IsChecked = false;
        }
    }

    internal static AppThemeStyle ResolveSelectedThemeStyle(
        bool? windows10Checked,
        bool? windows11Checked,
        AppThemeStyle currentStyle) =>
        windows11Checked == true
            ? AppThemeStyle.Windows11
            : windows10Checked == true
                ? AppThemeStyle.Windows10
                : currentStyle;
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
