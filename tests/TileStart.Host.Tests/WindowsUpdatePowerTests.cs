using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TileStart.Host.Controllers;
using TileStart.Host.Shell;

namespace TileStart.Host.Tests;

public sealed class WindowsUpdatePowerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsWindowsUpdateRestartRequirement(bool expected)
    {
        Assert.Equal(expected, WindowsUpdatePower.ReadRestartRequired(() => new FakeSystemInfo(expected)));
    }

    [Fact]
    public void PendingUpdateRequirementIsUsedWhenGlobalFlagIsFalse()
    {
        var session = new FakeUpdateSession(false, true);

        Assert.True(WindowsUpdatePower.ReadRestartRequired(
            () => new FakeSystemInfo(false),
            () => session));
        Assert.False(session.Searcher.Online);
        Assert.Equal("IsInstalled=0 and IsHidden=0", session.Searcher.Query);
    }

    [Fact]
    public void GlobalRestartRequirementSkipsPendingUpdateSearch()
    {
        var sessionCreated = false;

        Assert.True(WindowsUpdatePower.ReadRestartRequired(
            () => new FakeSystemInfo(true),
            () =>
            {
                sessionCreated = true;
                return new FakeUpdateSession();
            }));
        Assert.False(sessionCreated);
    }

    [Fact]
    public void MissingWindowsUpdateAgentIsNotReportedAsPending()
    {
        Assert.False(WindowsUpdatePower.ReadRestartRequired(() => null, () => null));
    }

    [Fact]
    public void UpdateShutdownFlagsInstallUpdatesWithoutForcingApplications()
    {
        Assert.Equal(
            WindowsUpdatePower.ShutdownInstallUpdates | WindowsUpdatePower.ShutdownPowerOff,
            WindowsUpdatePower.ShutdownFlags(restart: false));
        Assert.Equal(
            WindowsUpdatePower.ShutdownInstallUpdates | WindowsUpdatePower.ShutdownRestart,
            WindowsUpdatePower.ShutdownFlags(restart: true));
    }

    [Fact]
    public void SystemRestartRequirementSkipsFeatureUpdateCommit()
    {
        var sessionCreated = false;

        Assert.True(WindowsUpdatePower.PrepareUpdatesForShutdown(
            () => new FakeSystemInfo(true),
            () =>
            {
                sessionCreated = true;
                return new FakeUpdateSession();
            }));
        Assert.False(sessionCreated);
    }

    [Fact]
    public void PendingFeatureUpdateIsCommittedBeforeShutdown()
    {
        var session = new FakeUpdateSession();

        Assert.True(WindowsUpdatePower.PrepareUpdatesForShutdown(
            () => new FakeSystemInfo(false),
            () => session));
        Assert.Equal(0u, session.Installer.CommitFlags);
    }

    [Fact]
    public void MissingUpdateInstallerStopsOrdinaryRestart()
    {
        Assert.False(WindowsUpdatePower.PrepareUpdatesForShutdown(
            () => new FakeSystemInfo(false),
            () => null));
    }

    [Fact]
    public void PendingUpdateShowsBadgeAndBothPowerActions()
    {
        RunOnSta(() =>
        {
            var powerButton = new Button();
            var badge = new Border { Visibility = Visibility.Collapsed };
            var updateAndShutDown = new MenuItem { Visibility = Visibility.Collapsed };
            var updateAndRestart = new MenuItem { Visibility = Visibility.Collapsed };

            NavigationController.ApplyWindowsUpdateAvailability(
                powerButton,
                badge,
                updateAndShutDown,
                updateAndRestart,
                restartRequired: true);

            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Equal(Visibility.Visible, updateAndShutDown.Visibility);
            Assert.Equal(Visibility.Visible, updateAndRestart.Visibility);
            Assert.Equal("电源（Windows 更新需要重启）", powerButton.ToolTip);
        });
    }

    [Fact]
    public void CompletedUpdateHidesBadgeAndUpdatePowerActions()
    {
        RunOnSta(() =>
        {
            var powerButton = new Button();
            var badge = new Border { Visibility = Visibility.Visible };
            var updateAndShutDown = new MenuItem { Visibility = Visibility.Visible };
            var updateAndRestart = new MenuItem { Visibility = Visibility.Visible };

            NavigationController.ApplyWindowsUpdateAvailability(
                powerButton,
                badge,
                updateAndShutDown,
                updateAndRestart,
                restartRequired: false);

            Assert.Equal(Visibility.Collapsed, badge.Visibility);
            Assert.Equal(Visibility.Collapsed, updateAndShutDown.Visibility);
            Assert.Equal(Visibility.Collapsed, updateAndRestart.Visibility);
            Assert.Equal("电源", powerButton.ToolTip);
        });
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

    private sealed class FakeSystemInfo(bool rebootRequired)
    {
        public bool RebootRequired { get; } = rebootRequired;
    }

    private sealed class FakeUpdateSession(params bool[] rebootRequired)
    {
        public FakeUpdateSearcher Searcher { get; } = new(rebootRequired);
        public FakeUpdateInstaller Installer { get; } = new();

        public FakeUpdateSearcher CreateUpdateSearcher() => Searcher;

        public FakeUpdateInstaller CreateUpdateInstaller() => Installer;
    }

    private sealed class FakeUpdateInstaller
    {
        public uint? CommitFlags { get; private set; }

        public void Commit(uint flags) => CommitFlags = flags;
    }

    private sealed class FakeUpdateSearcher(bool[] rebootRequired)
    {
        public bool Online { get; set; } = true;
        public string Query { get; private set; } = string.Empty;

        public FakeSearchResult Search(string query)
        {
            Query = query;
            return new FakeSearchResult(rebootRequired);
        }
    }

    private sealed class FakeSearchResult(bool[] rebootRequired)
    {
        public FakeUpdateCollection Updates { get; } = new(rebootRequired);
    }

    private sealed class FakeUpdateCollection(bool[] rebootRequired)
    {
        public int Count => rebootRequired.Length;

        public FakeUpdate Item(int index) => new(rebootRequired[index]);
    }

    private sealed class FakeUpdate(bool rebootRequired)
    {
        public bool RebootRequired { get; } = rebootRequired;
    }
}
