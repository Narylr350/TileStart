using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace TileStart.Host.Backup;

public partial class BackupRestoreWindow : Window
{
    private readonly TileStartBackupService _service;
    private readonly Action<BackupRestoreRequest> _scheduleRestore;

    public BackupRestoreWindow(Action<BackupRestoreRequest> scheduleRestore, TileStartBackupService? service = null)
    {
        _scheduleRestore = scheduleRestore;
        _service = service ?? TileStartBackupService.Default;
        InitializeComponent();
    }

    private async void QuickBackup_Click(object sender, RoutedEventArgs e) =>
        await CreateBackupAsync(BackupComponents.Default);

    private async void CustomBackup_Click(object sender, RoutedEventArgs e) =>
        await CreateBackupAsync(GetSelectedComponents());

    private void QuickRestore_Click(object sender, RoutedEventArgs e) =>
        SelectAndScheduleRestore(useArchiveComponents: true);

    private void CustomRestore_Click(object sender, RoutedEventArgs e) =>
        SelectAndScheduleRestore(useArchiveComponents: false);

    private async Task CreateBackupAsync(BackupComponents components)
    {
        if (components == BackupComponents.None)
        {
            ShowWarning("请至少选择一项备份内容。");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "保存 TileStart 备份",
            Filter = "TileStart 备份|*.tilestartbackup",
            DefaultExt = ".tilestartbackup",
            AddExtension = true,
            FileName = $"TileStart-backup-{DateTime.Now:yyyyMMdd-HHmm}.tilestartbackup",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            SetBusy(true, "正在创建备份…");
            await Task.Run(() => _service.Create(dialog.FileName, components));
            var size = new FileInfo(dialog.FileName).Length;
            StatusText.Text = $"备份完成 · {FormatSize(size)}";
            MessageBox.Show(this, $"备份已保存到：\n{dialog.FileName}", "TileStart",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                              or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText.Text = "备份失败";
            MessageBox.Show(this, exception.Message, "无法创建备份", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SelectAndScheduleRestore(bool useArchiveComponents)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 TileStart 备份",
            Filter = "TileStart 备份|*.tilestartbackup|所有文件|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var inspection = _service.Inspect(dialog.FileName);
            var components = useArchiveComponents
                ? inspection.Components
                : inspection.Components & GetSelectedComponents();
            if (components == BackupComponents.None)
            {
                ShowWarning("这个备份不包含当前勾选的内容。");
                return;
            }

            var answer = MessageBox.Show(this,
                $"备份时间：{inspection.CreatedAt:yyyy-MM-dd HH:mm}\n" +
                $"将恢复：{DescribeComponents(components)}\n\n" +
                "TileStart 将关闭、创建当前状态的安全备份，然后恢复并自动重启。是否继续？",
                "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            StatusText.Text = "等待 TileStart 关闭后恢复…";
            _scheduleRestore(new BackupRestoreRequest(dialog.FileName, components));
            Close();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                              or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "无法读取备份", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private BackupComponents GetSelectedComponents()
    {
        var components = BackupComponents.None;
        AddIfChecked(LayoutBox, BackupComponents.Layout, ref components);
        AddIfChecked(CustomAppsBox, BackupComponents.CustomApplications, ref components);
        AddIfChecked(VisibilityBox, BackupComponents.ApplicationVisibility, ref components);
        AddIfChecked(PreferencesBox, BackupComponents.Preferences, ref components);
        AddIfChecked(ManagedIconsBox, BackupComponents.ManagedIcons, ref components);
        AddIfChecked(ExternalVisualsBox, BackupComponents.ExternalVisuals, ref components);
        AddIfChecked(TaskbarShortcutsBox, BackupComponents.TaskbarShortcuts, ref components);
        return components;
    }

    private static void AddIfChecked(System.Windows.Controls.CheckBox box, BackupComponents component,
        ref BackupComponents components)
    {
        if (box.IsChecked == true)
        {
            components |= component;
        }
    }

    private void SetBusy(bool busy, string status = "就绪")
    {
        QuickBackupButton.IsEnabled = !busy;
        QuickRestoreButton.IsEnabled = !busy;
        StatusText.Text = status;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "TileStart", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static string DescribeComponents(BackupComponents components)
    {
        var names = new List<string>();
        if (components.HasFlag(BackupComponents.Layout)) names.Add("磁贴布局");
        if (components.HasFlag(BackupComponents.CustomApplications)) names.Add("自定义应用");
        if (components.HasFlag(BackupComponents.ApplicationVisibility)) names.Add("应用可见性");
        if (components.HasFlag(BackupComponents.Preferences)) names.Add("窗口与导航偏好");
        if (components.HasFlag(BackupComponents.ManagedIcons)) names.Add("受管图标");
        if (components.HasFlag(BackupComponents.ExternalVisuals)) names.Add("外部图标与背景");
        if (components.HasFlag(BackupComponents.TaskbarShortcuts)) names.Add("任务栏辅助快捷方式");
        return string.Join("、", names);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:0.0} MB",
        >= 1024 => $"{bytes / 1024d:0.0} KB",
        _ => $"{bytes} B",
    };
}
