using System.IO;

namespace TileStart.Host.Tests;

public sealed class ShellHookSourceTests
{
    [Fact]
    public void NativeStartBypassRemainsSignaledAcrossDuplicateTaskListMessages()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "NativeSource",
            "ShellHook.cpp"));

        Assert.Contains(
            "CreateEventW(nullptr, TRUE, FALSE, kNativeStartBypassEventName)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "WaitForSingleObject(g_native_start_bypass_event, 0) == WAIT_OBJECT_0",
            source,
            StringComparison.Ordinal);
    }
}
