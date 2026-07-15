namespace TileStart.Host.Tests;

public sealed class StartWindowLifecycleTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ShouldHideAfterDeactivation_OnlyKeepsOwnedDialogsVisible(
        bool isActive,
        bool hasActiveOwnedWindow,
        bool expected)
    {
        Assert.Equal(expected, StartWindowLifecycle.ShouldHideAfterDeactivation(isActive, hasActiveOwnedWindow));
    }
}
