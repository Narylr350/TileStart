using System.Windows;
using System.Windows.Controls;
using TileStart.Host.Controllers;

namespace TileStart.Host.Tests;

public sealed class TileFolderVisualStateTests
{
    [Fact]
    public void CollapsedRegionStaysHiddenWhileHeldLayoutAnimationsAreReleased()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var region = new Border { Visibility = Visibility.Visible, Height = 200 };
                var container = new ContentPresenter { Content = region };
                Canvas.SetTop(container, 40);
                container.Measure(new Size(400, 400));
                container.Arrange(new Rect(0, 0, 400, 400));

                var resolvedContainer = TileWorkspaceController.FindTileFolderRegionContainer(region);

                Assert.Same(container, resolvedContainer);
                TileWorkspaceController.HoldTileFolderCollapseVisual(
                    region,
                    resolvedContainer,
                    Canvas.GetTop(resolvedContainer!));
                Assert.True(region.HasAnimatedProperties);
                Assert.True(container.HasAnimatedProperties);

                TileWorkspaceController.CompleteTileFolderCollapse(region, resolvedContainer);

                Assert.Equal(Visibility.Collapsed, region.Visibility);
                Assert.Equal(Visibility.Collapsed, region.ReadLocalValue(UIElement.VisibilityProperty));
                Assert.False(region.HasAnimatedProperties);
                Assert.False(container.HasAnimatedProperties);

                TileWorkspaceController.PrepareTileFolderExpansion(region);

                Assert.Equal(Visibility.Visible, region.Visibility);
                Assert.Equal(DependencyProperty.UnsetValue, region.ReadLocalValue(UIElement.VisibilityProperty));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "Tile folder visual-state test thread timed out.");
        Assert.Null(failure);
    }
}