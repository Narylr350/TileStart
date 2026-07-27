using System.IO;

namespace TileStart.Host.Tests;

public sealed class ShellCompatibilitySourceTests
{
    private static readonly string InjectorSource = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Native",
        "InjectorMain.cpp");

    [Fact]
    public void InjectorChoosesAnAdapterFamilyWithoutAnExactBuildGate()
    {
        var source = File.ReadAllText(InjectorSource);

        Assert.Contains("build < 22000", source, StringComparison.Ordinal);
        Assert.Contains("build < 26100", source, StringComparison.Ordinal);
        Assert.Contains("compatibility fallback", source, StringComparison.Ordinal);
        Assert.DoesNotContain("not supported for Shell injection", source, StringComparison.OrdinalIgnoreCase);
    }
}
