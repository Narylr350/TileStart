namespace TileStart.Host.Tests;

public sealed class StartWindowLifecycleTests
{
    [Fact]
    public void WaitingForForegroundNeverTreatsAnExternalWindowAsDismissal()
    {
        var lifecycle = new StartWindowLifecycle();

        for (var index = 0; index < 10; index++)
        {
            Assert.False(lifecycle.ObserveForeground(true, false, false, false));
        }

        Assert.False(lifecycle.HasAcquiredForeground);
        Assert.Equal(0, lifecycle.ForeignForegroundObservations);
    }

    [Fact]
    public void NativeActivationArmsDismissalAfterConsecutiveForeignObservations()
    {
        var lifecycle = new StartWindowLifecycle();
        lifecycle.ObserveNativeActivation();

        Assert.False(lifecycle.ObserveForeground(true, false, false, false));
        Assert.False(lifecycle.ObserveForeground(true, false, false, false));
        Assert.True(lifecycle.ObserveForeground(true, false, false, false));
    }

    [Fact]
    public void ForegroundOwnershipAcquiresAndResetsForeignObservationCount()
    {
        var lifecycle = new StartWindowLifecycle();
        lifecycle.ObserveNativeActivation();
        Assert.False(lifecycle.ObserveForeground(true, false, false, false));

        Assert.False(lifecycle.ObserveForeground(true, true, false, false));

        Assert.True(lifecycle.HasAcquiredForeground);
        Assert.Equal(0, lifecycle.ForeignForegroundObservations);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OwnedWindowsAndContextMenusCancelTransientForeignObservations(
        bool hasActiveOwnedWindow,
        bool hasOpenContextMenu)
    {
        var lifecycle = new StartWindowLifecycle();
        lifecycle.ObserveNativeActivation();
        Assert.False(lifecycle.ObserveForeground(true, false, false, false));

        Assert.False(lifecycle.ObserveForeground(
            true,
            false,
            hasActiveOwnedWindow,
            hasOpenContextMenu));

        Assert.Equal(0, lifecycle.ForeignForegroundObservations);
    }

    [Fact]
    public void ResetReturnsLifecycleToWaitingState()
    {
        var lifecycle = new StartWindowLifecycle();
        lifecycle.ObserveNativeActivation();
        lifecycle.ObserveForeground(true, false, false, false);

        lifecycle.Reset();

        Assert.False(lifecycle.HasAcquiredForeground);
        Assert.Equal(0, lifecycle.ForeignForegroundObservations);
    }
}
