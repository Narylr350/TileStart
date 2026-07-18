namespace TileStart.Host.Tests;

public sealed class StartWindowLifecycleTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    public void HasAcquiredForegroundAcceptsSuccessfulRequestOrObservedOwnership(
        bool alreadyAcquired,
        bool setForegroundSucceeded,
        bool foregroundBelongsToStart,
        bool expected)
    {
        Assert.Equal(expected, StartWindowLifecycle.HasAcquiredForeground(
            alreadyAcquired,
            setForegroundSucceeded,
            foregroundBelongsToStart));
    }

    [Theory]
    [InlineData(true, true, false, false, false, true)]
    [InlineData(false, true, false, false, false, false)]
    [InlineData(true, false, false, false, false, false)]
    [InlineData(true, true, true, false, false, false)]
    [InlineData(true, true, false, true, false, false)]
    [InlineData(true, true, false, false, true, false)]
    public void ShouldHideForForegroundChange_OnlyHidesAfterConfirmedAcquisition(
        bool hasAcquiredForeground,
        bool foregroundKnown,
        bool foregroundBelongsToStart,
        bool hasActiveOwnedWindow,
        bool hasOpenContextMenu,
        bool expected)
    {
        Assert.Equal(expected, StartWindowLifecycle.ShouldHideForForegroundChange(
            hasAcquiredForeground,
            foregroundKnown,
            foregroundBelongsToStart,
            hasActiveOwnedWindow,
            hasOpenContextMenu));
    }
}
