using TileStart.Host.Controllers;

namespace TileStart.Host.Tests;

public sealed class ControllerLifecycleTests
{
    [Fact]
    public void LongLivedControllersExposeExplicitLifetime()
    {
        Type[] controllerTypes =
        [
            typeof(StartWindowController),
            typeof(ApplicationPaneController),
            typeof(NavigationController),
            typeof(TileDragCoordinator),
            typeof(TileWorkspaceController),
        ];

        Assert.All(controllerTypes, type => Assert.True(
            typeof(IDisposable).IsAssignableFrom(type),
            $"{type.Name} must release timers, event subscriptions, and pending callbacks explicitly."));
    }
}