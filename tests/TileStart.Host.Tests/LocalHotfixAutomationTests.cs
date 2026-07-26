using System.IO;

namespace TileStart.Host.Tests;

public sealed class LocalHotfixAutomationTests
{
    [Fact]
    public void WorkflowBuildsAnArtifactWithoutCreatingARelease()
    {
        var workflow = ReadAutomationFile("local-hotfix.yml");

        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("retention-days: 14", workflow);
        Assert.Contains("name: TileStart-local-hotfix", workflow);
        Assert.Contains("-InformationalVersion $env:INFORMATIONAL_VERSION", workflow);
        Assert.DoesNotContain("gh release create", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git tag", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackageBuildCanStampAStableVersionWithHotfixMetadata()
    {
        var script = ReadAutomationFile("Build-Package.ps1");

        Assert.Contains("[string]$InformationalVersion", script);
        Assert.Contains("/p:InformationalVersion=$InformationalVersion", script);
        Assert.Contains("-p:InformationalVersion=$InformationalVersion", script);
    }

    [Fact]
    public void InstallerVerifiesAndRestartsTheInstalledCopy()
    {
        var script = ReadAutomationFile("Install-Local-Hotfix.ps1");

        Assert.Contains("Get-FileHash", script);
        Assert.Contains("TrimStart('*')", script);
        Assert.Contains("& $installedHost --shutdown", script);
        Assert.Contains("LOCAL-HOTFIX.json", script);
        Assert.Contains("$env:ProgramFiles", script);
        Assert.Contains("Start-Process -FilePath $installedHost", script);
    }

    private static string ReadAutomationFile(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Automation", fileName));
}
