using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class HostRequestTests
{
    [Fact]
    public void AddApplicationArgumentsCreatePathRequest()
    {
        var request = HostRequest.FromArguments(["--add-app-list", @"D:\工具\Portable.exe"]);

        Assert.Equal(HostRequestKind.AddToAppList, request.Kind);
        Assert.Equal(@"D:\工具\Portable.exe", request.Path);
    }

    [Fact]
    public void PinTileRequestRoundTripsUnicodePath()
    {
        var expected = new HostRequest(HostRequestKind.PinTile, @"D:\便携软件\启动器.lnk");

        Assert.True(HostRequest.TryDecode(expected.Encode(), out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("OPEN", HostRequestKind.Open)]
    [InlineData("EXIT", HostRequestKind.Exit)]
    public void LegacyPipeCommandsRemainSupported(string command, HostRequestKind expectedKind)
    {
        Assert.True(HostRequest.TryDecode(System.Text.Encoding.UTF8.GetBytes(command), out var request));
        Assert.Equal(expectedKind, request.Kind);
    }

    [Fact]
    public void CustomApplicationsSupportPortableExecutable()
    {
        Assert.True(CustomAppStore.Supports(Environment.ProcessPath!));
    }
}
