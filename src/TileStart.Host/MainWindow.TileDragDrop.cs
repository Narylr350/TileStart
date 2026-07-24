using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TileStart.Host.Applications;

namespace TileStart.Host;

public partial class MainWindow
{
    internal static BitmapSource CaptureElement(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96 * dpi.DpiScaleX,
            96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    private void AppButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _tileDragCoordinator?.AppButton_PreviewMouseLeftButtonDown(sender, e);

    private void AppButton_PreviewMouseMove(object sender, MouseEventArgs e) =>
        _tileDragCoordinator?.AppButton_PreviewMouseMove(sender, e);

    private void GroupHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _tileDragCoordinator?.GroupHeader_PreviewMouseLeftButtonDown(sender, e);

    private void GroupHeader_PreviewMouseMove(object sender, MouseEventArgs e) =>
        _tileDragCoordinator?.GroupHeader_PreviewMouseMove(sender, e);

    private void GroupHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _tileDragCoordinator?.GroupHeader_PreviewMouseLeftButtonUp(sender, e);

    private void GroupHeader_LostMouseCapture(object sender, MouseEventArgs e) =>
        _tileDragCoordinator?.GroupHeader_LostMouseCapture(sender, e);

    private void TileButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _tileDragCoordinator?.TileButton_PreviewMouseLeftButtonDown(sender, e);

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _tileDragCoordinator?.Window_PreviewMouseLeftButtonUp(sender, e);

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e) =>
        _tileDragCoordinator?.Window_PreviewMouseMove(sender, e);

    private void TileGroup_DragOver(object sender, System.Windows.DragEventArgs e) =>
        _tileDragCoordinator?.TileGroup_DragOver(sender, e);

    private void TileGroup_Drop(object sender, System.Windows.DragEventArgs e) =>
        _tileDragCoordinator?.TileGroup_Drop(sender, e);

    private void FolderRegion_DragOver(object sender, System.Windows.DragEventArgs e) =>
        _tileDragCoordinator?.FolderRegion_DragOver(sender, e);

    private void FolderRegion_Drop(object sender, System.Windows.DragEventArgs e) =>
        _tileDragCoordinator?.FolderRegion_Drop(sender, e);

    private void TileArea_DragOver(object sender, System.Windows.DragEventArgs e) =>
        _tileDragCoordinator?.TileArea_DragOver(sender, e);

    private void TileArea_Drop(object sender, System.Windows.DragEventArgs e) =>
        _tileDragCoordinator?.TileArea_Drop(sender, e);

    private void OpenAppFileLocation_Click(object sender, RoutedEventArgs e) =>
        _tileDragCoordinator?.OpenAppFileLocation_Click(sender, e);
}
