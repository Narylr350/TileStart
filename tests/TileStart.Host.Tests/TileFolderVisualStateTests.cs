using System.IO;

namespace TileStart.Host.Tests;

public sealed class TileFolderVisualStateTests
{
    private static readonly string ControllerSource = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "HostSource",
        "Controllers",
        "TileWorkspaceController.cs");

    [Fact]
    public void FolderAnimationUsesTwoLiveClippedLayersInsteadOfScreenshotMorphing()
    {
        var source = File.ReadAllText(ControllerSource);

        Assert.Contains("preview.RenderTransform = transform", source, StringComparison.Ordinal);
        Assert.Contains("TilePreviewExitDurationMilliseconds", source, StringComparison.Ordinal);
        Assert.Contains("TilePreviewEnterDurationMilliseconds", source, StringComparison.Ordinal);
        Assert.Contains("-(tile.Top + tile.PixelHeight)", source, StringComparison.Ordinal);
        Assert.Contains("TileChildWaveDelay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow.CaptureElement", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FolderAnimationOverlay", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpandedChildrenKeepFullSizeAndOnlyAnimateVertically()
    {
        var source = File.ReadAllText(ControllerSource);
        var methodStart = source.IndexOf("private int AnimateTileFolderChildren", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private TileFolderPreviewTransition?", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("TranslateTransform.YProperty", method, StringComparison.Ordinal);
        Assert.DoesNotContain("TranslateTransform.XProperty", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ScaleTransform", method, StringComparison.Ordinal);
        Assert.DoesNotContain("OpacityProperty", method, StringComparison.Ordinal);
    }
}
