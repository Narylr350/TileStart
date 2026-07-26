using TileStart.Host;

namespace TileStart.Host.Tests;

public sealed class AppRestartTests
{
    [Theory]
    [InlineData(new[] { "--wait-for-process", "1234" }, 1234)]
    [InlineData(new[] { "--WAIT-FOR-PROCESS", "42" }, 42)]
    [InlineData(new[] { "--wait-for-process", "invalid" }, null)]
    [InlineData(new[] { "--wait-for-process", "0" }, null)]
    [InlineData(new string[0], null)]
    public void ReadsPreviousProcessId(string[] arguments, int? expected)
    {
        Assert.Equal(expected, App.ReadWaitProcessId(arguments));
    }
}
