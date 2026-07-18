namespace TileStart.Host.Tests;

public sealed class StartMotionTests
{
    [Theory]
    [InlineData(0, 60)]
    [InlineData(0.5, 115)]
    [InlineData(1, 170)]
    public void CalculateEntrance_UsesRecoveredBottomTaskbarFallback(double position, double fromY)
    {
        var result = StartMotion.CalculateEntrance(position, true);

        Assert.Equal(fromY, result.FromY);
        Assert.Equal(17, result.DelayMilliseconds);
        Assert.Equal(500, result.DurationMilliseconds);
    }

    [Fact]
    public void StageEntranceSupportsAnimatingTheWholeSurfaceAsOneTarget()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var surface = new System.Windows.Controls.Grid { Width = 100, Height = 100 };
                surface.Measure(new System.Windows.Size(100, 100));
                surface.Arrange(new System.Windows.Rect(0, 0, 100, 100));

                StartMotion.StageEntrance(surface, [surface], bottomTaskbar: true, animationsEnabled: true);

                var transform = Assert.IsType<System.Windows.Media.TranslateTransform>(surface.RenderTransform);
                Assert.Equal(60, transform.Y);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void PlayEntranceCompletesImmediatelyWhenNoAnimationIsPrepared()
    {
        Exception? failure = null;
        var completed = false;
        var thread = new Thread(() =>
        {
            try
            {
                var surface = new System.Windows.Controls.Grid();

                StartMotion.PlayEntrance([surface], animationsEnabled: true, () => completed = true);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.True(completed);
    }

    [Fact]
    public void CalculateEntrance_DoesNotInventVerticalMotionForOtherTaskbarEdges()
    {
        var result = StartMotion.CalculateEntrance(0.5, false);

        Assert.Equal(0, result.FromY);
        Assert.Equal(0, result.DurationMilliseconds);
    }
}
