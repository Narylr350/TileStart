using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class WinKeyHookTests
{
    private const uint LeftWin = 0x5B;
    private const uint RightWin = 0x5C;
    private const uint Shift = 0x10;
    private const uint E = 0x45;
    private const uint S = 0x53;

    [Theory]
    [InlineData(LeftWin)]
    [InlineData(RightWin)]
    public void StandaloneWinKeyOpensTileStartOnRelease(uint winKey)
    {
        using var hook = new WinKeyHook(() => { });

        Assert.Equal(WinKeyAction.Suppress, hook.ProcessKey(winKey, keyDown: true, keyUp: false));
        Assert.Equal(
            WinKeyAction.Suppress | WinKeyAction.OpenTileStart,
            hook.ProcessKey(winKey, keyDown: false, keyUp: true));
    }

    [Fact]
    public void WinShortcutReinjectsChordAndDoesNotOpenTileStart()
    {
        using var hook = new WinKeyHook(() => { });

        Assert.Equal(WinKeyAction.Suppress, hook.ProcessKey(LeftWin, keyDown: true, keyUp: false));
        Assert.Equal(
            WinKeyAction.Suppress | WinKeyAction.InjectWinDown | WinKeyAction.InjectCurrentKeyDown,
            hook.ProcessKey(E, keyDown: true, keyUp: false));
        Assert.Equal(WinKeyAction.None, hook.ProcessKey(E, keyDown: false, keyUp: true));
        Assert.Equal(
            WinKeyAction.Suppress | WinKeyAction.InjectWinUp,
            hook.ProcessKey(LeftWin, keyDown: false, keyUp: true));
    }

    [Fact]
    public void MultiKeyShortcutOnlyReinjectsFirstChordKey()
    {
        using var hook = new WinKeyHook(() => { });

        hook.ProcessKey(LeftWin, keyDown: true, keyUp: false);
        Assert.Equal(
            WinKeyAction.Suppress | WinKeyAction.InjectWinDown | WinKeyAction.InjectCurrentKeyDown,
            hook.ProcessKey(Shift, keyDown: true, keyUp: false));
        Assert.Equal(WinKeyAction.None, hook.ProcessKey(S, keyDown: true, keyUp: false));
        Assert.Equal(WinKeyAction.None, hook.ProcessKey(S, keyDown: false, keyUp: true));
        Assert.Equal(WinKeyAction.None, hook.ProcessKey(Shift, keyDown: false, keyUp: true));
        Assert.Equal(
            WinKeyAction.Suppress | WinKeyAction.InjectWinUp,
            hook.ProcessKey(LeftWin, keyDown: false, keyUp: true));
    }

    [Fact]
    public void UnrelatedKeysPassThroughWhenWinIsNotHeld()
    {
        using var hook = new WinKeyHook(() => { });

        Assert.Equal(WinKeyAction.None, hook.ProcessKey(E, keyDown: true, keyUp: false));
        Assert.Equal(WinKeyAction.None, hook.ProcessKey(E, keyDown: false, keyUp: true));
    }
}
