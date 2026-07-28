using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using WpfApplication = System.Windows.Application;

namespace TileStart.Host.Updates;

public partial class UpdateProgressWindow : Window
{
    private readonly CancellationTokenSource _userCancellation = new();
    private Func<IProgress<UpdateProgressInfo>, CancellationToken, Task<DownloadedUpdate>>? _operation;
    private CancellationToken _externalCancellation;
    private DownloadedUpdate? _result;
    private Exception? _error;
    private bool _operationCompleted;

    public UpdateProgressWindow(Window? owner = null)
    {
        InitializeComponent();
        owner ??= WpfApplication.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            Owner = owner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public bool UserCanceled { get; private set; }

    public DownloadedUpdate Run(
        Func<IProgress<UpdateProgressInfo>, CancellationToken, Task<DownloadedUpdate>> operation,
        CancellationToken cancellationToken)
    {
        _operation = operation;
        _externalCancellation = cancellationToken;
        _ = ShowDialog();

        if (_error is not null)
        {
            ExceptionDispatchInfo.Capture(_error).Throw();
        }

        return _result ?? throw new InvalidOperationException("更新下载未返回结果。");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_operation is null)
        {
            return;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _externalCancellation,
            _userCancellation.Token);
        try
        {
            var progress = new Progress<UpdateProgressInfo>(UpdateProgress);
            _result = await _operation(progress, cancellation.Token);
        }
        catch (Exception exception)
        {
            _error = exception;
        }
        finally
        {
            _operationCompleted = true;
            Close();
        }
    }

    private void UpdateProgress(UpdateProgressInfo progress)
    {
        StatusText.Text = progress.Stage switch
        {
            UpdateProgressStage.DownloadingChecksums => "正在获取校验信息…",
            UpdateProgressStage.DownloadingPackage => "正在下载更新包…",
            UpdateProgressStage.VerifyingPackage => "正在校验更新包…",
            UpdateProgressStage.PreparingInstall => "更新包已通过校验",
            _ => "正在准备更新…",
        };
        StageText.Text = progress.Stage switch
        {
            UpdateProgressStage.DownloadingChecksums => "获取 SHA-256 校验文件",
            UpdateProgressStage.DownloadingPackage => "从 GitHub 下载",
            UpdateProgressStage.VerifyingPackage => "验证 SHA-256",
            UpdateProgressStage.PreparingInstall => "准备下一步",
            _ => string.Empty,
        };

        if (progress.Stage == UpdateProgressStage.DownloadingPackage && progress.Percentage is { } percentage)
        {
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = percentage;
            PercentageText.Text = $"{percentage:0}%";
            DetailText.Text = $"{FormatBytes(progress.BytesReceived)} / {FormatBytes(progress.TotalBytes!.Value)}";
        }
        else
        {
            DownloadProgress.IsIndeterminate = progress.Stage != UpdateProgressStage.PreparingInstall;
            DownloadProgress.Value = progress.Stage == UpdateProgressStage.PreparingInstall ? 100 : 0;
            PercentageText.Text = progress.Stage == UpdateProgressStage.PreparingInstall ? "100%" : string.Empty;
            DetailText.Text = progress.Stage switch
            {
                UpdateProgressStage.DownloadingPackage when progress.BytesReceived > 0 =>
                    $"已下载 {FormatBytes(progress.BytesReceived)}",
                UpdateProgressStage.VerifyingPackage => "正在确认文件完整性",
                UpdateProgressStage.PreparingInstall => "下载和 SHA-256 校验均已完成",
                _ => "即将开始下载",
            };
        }
    }

    internal static string FormatBytes(long bytes)
    {
        const double unit = 1024d;
        if (bytes >= unit * unit)
        {
            return $"{bytes / unit / unit:0.0} MB";
        }

        if (bytes >= unit)
        {
            return $"{bytes / unit:0.0} KB";
        }

        return $"{bytes} B";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationCompleted || UserCanceled)
        {
            return;
        }

        UserCanceled = true;
        CancelButton.IsEnabled = false;
        StatusText.Text = "正在取消…";
        DetailText.Text = "正在停止下载并清理临时文件";
        _userCancellation.Cancel();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_operationCompleted)
        {
            return;
        }

        e.Cancel = true;
        CancelButton_Click(this, new RoutedEventArgs());
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
