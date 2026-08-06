using System.IO;

namespace TileStart.Host.Tests;

public sealed class ReleaseAutomationTests
{
    [Fact]
    public void WorkflowSeparatesValidationBuildAndPublication()
    {
        var workflow = ReadWorkflow();

        Assert.Contains("run-name: Release", workflow);
        Assert.Contains("  prepare:", workflow);
        Assert.Contains("  build:", workflow);
        Assert.Contains("    needs: prepare", workflow);
        Assert.Contains("  publish:", workflow);
        Assert.Contains("    needs: [prepare, build]", workflow);
        Assert.Contains("timeout-minutes: 45", workflow);
        Assert.Contains("timeout-minutes: 10", workflow);
    }

    [Fact]
    public void WorkflowPinsActionsAndLimitsArtifactRetention()
    {
        var workflow = ReadWorkflow();

        Assert.Contains("actions/checkout@11d5960a326750d5838078e36cf38b85af677262", workflow);
        Assert.Contains("actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9", workflow);
        Assert.Contains("actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02", workflow);
        Assert.Contains("actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093", workflow);
        Assert.DoesNotContain("actions/checkout@v", workflow);
        Assert.DoesNotContain("actions/setup-dotnet@v", workflow);
        Assert.DoesNotContain("actions/upload-artifact@v", workflow);
        Assert.DoesNotContain("actions/download-artifact@v", workflow);
        Assert.Contains("retention-days: 7", workflow);
    }

    [Fact]
    public void WorkflowVerifiesAssetsBeforePublishingADraftRelease()
    {
        var workflow = ReadWorkflow();

        var verify = workflow.IndexOf("sha256sum --check SHA256SUMS.txt", StringComparison.Ordinal);
        var draft = workflow.IndexOf("--draft", verify, StringComparison.Ordinal);
        var publish = workflow.IndexOf("--draft=false", draft, StringComparison.Ordinal);

        Assert.True(verify >= 0 && draft > verify && publish > draft);
        Assert.Contains("Delete incomplete draft", workflow);
        Assert.Contains("--cleanup-tag", workflow);
        Assert.Contains("git tag --list $tag", workflow);
    }

    [Fact]
    public void WorkflowUsesLeastPrivilegePerJob()
    {
        var workflow = ReadWorkflow().Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.DoesNotContain("\npermissions:\n  contents: write", workflow, StringComparison.Ordinal);
        Assert.Equal(2, workflow.Split("      contents: read", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, workflow.Split("      contents: write", StringSplitOptions.None).Length - 1);
    }

    private static string ReadWorkflow() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Automation", "release.yml"));
}
