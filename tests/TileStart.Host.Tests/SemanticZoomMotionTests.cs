namespace TileStart.Host.Tests;

public sealed class SemanticZoomMotionTests
{
    [Fact]
    public void ZoomedInRestingStateUsesSharedTwoTimesSurfaceAndHalfScaleCorrection()
    {
        var viewport = new System.Windows.Size(252, 720);

        var surface = SemanticZoomMotion.CalculateSurfaceState(viewport, zoomedInViewActive: true);
        var correction = SemanticZoomMotion.CalculateZoomedInPresenterCorrection(viewport);

        Assert.Equal(new SemanticZoomTransform(2, -126, -360), surface);
        Assert.Equal(new SemanticZoomTransform(0.5, 63, 180), correction);
    }

    [Fact]
    public void SharedSurfaceAndZoomedInCorrectionComposeToIdentityForTheFullBounds()
    {
        var viewport = new System.Windows.Size(252, 720);
        var bounds = new System.Windows.Rect(8, 144, 244, 36);
        var correction = SemanticZoomMotion.CalculateZoomedInPresenterCorrection(viewport);
        var surface = SemanticZoomMotion.CalculateSurfaceState(viewport, zoomedInViewActive: true);

        var rendered = surface.Transform(correction.Transform(bounds));

        Assert.Equal(bounds.X, rendered.X, 6);
        Assert.Equal(bounds.Y, rendered.Y, 6);
        Assert.Equal(bounds.Width, rendered.Width, 6);
        Assert.Equal(bounds.Height, rendered.Height, 6);
    }

    [Fact]
    public void ZoomedOutPresenterContractsFromTwiceSizeAroundTheViewportCenter()
    {
        var viewport = new System.Windows.Size(252, 720);
        var letterBounds = new System.Windows.Rect(22, 156, 48, 48);
        var zoomedInSurface = SemanticZoomMotion.CalculateSurfaceState(viewport, zoomedInViewActive: true);
        var zoomedOutSurface = SemanticZoomMotion.CalculateSurfaceState(viewport, zoomedInViewActive: false);

        var start = zoomedInSurface.Transform(letterBounds);
        var end = zoomedOutSurface.Transform(letterBounds);

        Assert.Equal(-82, start.X, 6);
        Assert.Equal(-48, start.Y, 6);
        Assert.Equal(96, start.Width, 6);
        Assert.Equal(96, start.Height, 6);
        Assert.Equal(letterBounds, end);
    }

    [Fact]
    public void PresenterFadeUsesRecoveredWin10ThemeDuration()
    {
        Assert.Equal(167, SemanticZoomMotion.PresenterFadeDurationMilliseconds);
        Assert.Equal(350, SemanticZoomMotion.ViewChangeDurationMilliseconds);
    }
}