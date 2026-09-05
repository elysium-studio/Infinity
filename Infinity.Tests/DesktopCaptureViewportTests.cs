using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopCaptureViewportTests
{
    private static readonly DesktopCaptureViewport Viewport = DesktopCaptureViewport.Create(1920, 1080, 1920, 1040, 0, 0, 0.38);

    [Theory]
    [InlineData(-1200)]
    [InlineData(0)]
    [InlineData(3000)]
    public void IncludesVisibleAndPartiallyVisibleThumbnails(double x) => Assert.True(Viewport.ShouldCapture(x, 100, 500, 400, false));

    [Fact]
    public void PrewarmsHalfAPageBeyondEitherScreenEdge()
    {
        Assert.True(Viewport.ShouldCapture(Viewport.Right + 400, 0, 200, 400, false));
        Assert.True(Viewport.ShouldCapture(Viewport.Left - 600, 0, 200, 400, false));
        Assert.False(Viewport.ShouldCapture(Viewport.Right + 1000, 0, 200, 400, false));
        Assert.False(Viewport.ShouldCapture(Viewport.Left - 1200, 0, 200, 400, false));
    }


    [Fact]
    public void DistantPagesDoNotCapture()
    {
        Assert.False(Viewport.ShouldCapture(10000, 0, 1920, 1040, false));
        Assert.False(Viewport.ShouldCapture(-10000, 0, 1920, 1040, false));
    }


    [Fact]
    public void RetentionBandAvoidsRestartingAtThePrewarmBoundary()
    {
        double x = Viewport.Right + Viewport.Prewarm + 100;
        Assert.False(Viewport.ShouldCapture(x, 0, 200, 400, false));
        Assert.True(Viewport.ShouldCapture(x, 0, 200, 400, true));
        Assert.False(Viewport.ShouldCapture(x + Viewport.Retention, 0, 200, 400, true));
    }


    [Fact]
    public void DoesNotCaptureOutsideVerticalScreenBounds()
    {
        Assert.False(Viewport.ShouldCapture(0, Viewport.Bottom + 1, 500, 400, false));
        Assert.False(Viewport.ShouldCapture(0, Viewport.Top - 401, 500, 400, false));
    }


    [Theory]
    [InlineData(0, 40, 1920, 1040)]
    [InlineData(0, 0, 1920, 1040)]
    [InlineData(40, 0, 1880, 1080)]
    [InlineData(0, 0, 1880, 1080)]
    public void AccountsForTaskbarOffsetsAndTheWholeZoomPath(double offsetX, double offsetY, double workWidth, double workHeight)
    {
        DesktopCaptureViewport viewport = DesktopCaptureViewport.Create(1920, 1080, workWidth, workHeight, offsetX, offsetY, 0.38);
        foreach (double scale in new[]
        {
            0.38,
            0.6,
            1.0
        }

        )
        {
            double left = workWidth / 2 + (-offsetX - workWidth / 2) / scale;
            double top = workHeight / 2 + (-offsetY - workHeight / 2) / scale;
            double right = workWidth / 2 + (1920 - offsetX - workWidth / 2) / scale;
            double bottom = workHeight / 2 + (1080 - offsetY - workHeight / 2) / scale;
            Assert.True(viewport.ShouldCapture(left, top, 1, 1, false));
            Assert.True(viewport.ShouldCapture(right - 1, bottom - 1, 1, 1, false));
        }
    }


    [Fact]
    public void InvalidOrUninitialisedGeometryDoesNotStartCapture()
    {
        Assert.False(default(DesktopCaptureViewport).ShouldCapture(0, 0, 100, 100, false));
        Assert.False(Viewport.ShouldCapture(double.NaN, 0, 100, 100, false));
        Assert.False(Viewport.ShouldCapture(0, 0, 0, 100, false));
        Assert.False(Viewport.ShouldCapture(0, 0, 100, double.PositiveInfinity, false));
        Assert.Equal(default, DesktopCaptureViewport.Create(1920, 1080, 1920, 1040, 0, 0, 0));
    }
}
