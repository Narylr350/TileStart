using System.Threading;
using System.Windows;
using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class TileGroupHeaderTests
{
    [Fact]
    public void XamlCanBeInstantiatedAndMeasuredOnStaThread()
    {
        RunOnSta(() =>
        {
            var header = new TileGroupHeader
            {
                DataContext = new TileGroup { Name = "工具" },
            };
            header.Measure(new Size(Win10TileMetrics.GroupWidth, Win10VisualMetrics.TileGroupHeaderHeight));
        });
    }

    [Fact]
    public void UnnamedGroupPlaceholderIsHiddenAtRest()
    {
        RunOnSta(() =>
        {
            var header = new TileGroupHeader
            {
                DataContext = new TileGroup(),
            };

            var titleHost = Assert.IsType<System.Windows.Controls.Border>(header.FindName("NameTextBlockHost"));
            Assert.Equal(Visibility.Collapsed, titleHost.Visibility);
        });
    }

    [Fact]
    public void TitleAlignsWithTheNativeCenteredHeaderBaseline()
    {
        RunOnSta(() =>
        {
            var header = new TileGroupHeader
            {
                DataContext = new TileGroup { Name = "工具" },
            };

            var title = Assert.IsType<System.Windows.Controls.TextBlock>(header.FindName("NameTextBlock"));
            Assert.Equal(VerticalAlignment.Center, title.VerticalAlignment);
            Assert.Equal(new Thickness(), Win10VisualMetrics.TileGroupTitleInteractiveMargin);
        });
    }

    [Fact]
    public void ScreenshotCalibrationKeepsAThreeDipGapBetweenHeaderAndTiles()
    {
        Assert.Equal(3, Win10VisualMetrics.TileGroupTilesMargin.Top);
        Assert.Equal(Win10VisualMetrics.TileNestedPanelMargin.Left, Win10VisualMetrics.TileGroupTilesMargin.Left);
        Assert.Equal(Win10VisualMetrics.TileNestedPanelMargin.Right, Win10VisualMetrics.TileGroupTilesMargin.Right);
        Assert.Equal(Win10VisualMetrics.TileNestedPanelMargin.Bottom, Win10VisualMetrics.TileGroupTilesMargin.Bottom);
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(error);
    }
}