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
                var region = new Border { Visibility = Visibility.Visible };
                var container = new ContentPresenter();

                TileWorkspaceController.CompleteTileFolderCollapse(region, container);

                Assert.Equal(Visibility.Collapsed, region.Visibility);
                Assert.Equal(Visibility.Collapsed, region.ReadLocalValue(UIElement.VisibilityProperty));

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
