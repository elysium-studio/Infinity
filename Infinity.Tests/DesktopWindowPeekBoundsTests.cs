using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowPeekBoundsTests
{
    [Fact]
    public void EnlargedPreviewIsCenteredAndPreservesAspectRatio()
    {
        DesktopWindowPeekBounds target = DesktopWindowPeekBounds.Center(2000, 1000, 1000, 700);
        Assert.Equal(new(40, 120, 920, 460), target);
    }

    [Theory]
    [InlineData(100, 160)]
    [InlineData(600, 200)]
    [InlineData(-50, 80)]
    public void AnimationStartsAtThumbnailCoordinatesNotScreenCenter(double x, double y)
    {
        DesktopWindowPeekBounds source = new(x, y, 400, 200);
        DesktopWindowPeekBounds target = DesktopWindowPeekBounds.Center(2000, 1000, 1000, 700);
        Assert.Equal(source, source.Fit(target));
    }

    [Fact]
    public void DifferentThumbnailAspectRatioDoesNotDistortImage()
    {
        DesktopWindowPeekBounds source = new(100, 200, 300, 300);
        DesktopWindowPeekBounds target = DesktopWindowPeekBounds.Center(2000, 1000, 1000, 700);
        DesktopWindowPeekBounds start = source.Fit(target);
        Assert.Equal(new(100, 275, 300, 150), start);
        Assert.Equal(target.Width / target.Height, start.Width / start.Height);
    }

    [Fact]
    public void InvalidSourceFallsBackToTarget()
    {
        DesktopWindowPeekBounds target = DesktopWindowPeekBounds.Center(2000, 1000, 1000, 700);
        Assert.Equal(target, default(DesktopWindowPeekBounds).Fit(target));
        Assert.False(DesktopWindowPeekBounds.Center(0, 1000, 1000, 700).IsValid);
        Assert.False(DesktopWindowPeekBounds.Center(2000, 1000, double.NaN, 700).IsValid);
    }
}
