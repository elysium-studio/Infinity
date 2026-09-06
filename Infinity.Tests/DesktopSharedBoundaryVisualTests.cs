using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopSharedBoundaryVisualTests
{
    [Fact]
    public void VerticalStripSpansTheEntireSharedBoundary()
    {
        DesktopSharedBoundaryMember[] group = [new(1, new(0, 0, 500, 800), DesktopResizeEdge.Right), new(2, new(500, 0, 500, 800), DesktopResizeEdge.Left)];
        Assert.Equal(new(494, 0, 12, 800), DesktopSharedBoundaryVisual.GetBounds(group, 12));
    }

    [Fact]
    public void HorizontalStripSpansTheEntireSharedBoundary()
    {
        DesktopSharedBoundaryMember[] group = [new(1, new(0, 0, 1000, 400), DesktopResizeEdge.Bottom), new(2, new(0, 400, 1000, 400), DesktopResizeEdge.Top)];
        Assert.Equal(new(0, 394, 1000, 12), DesktopSharedBoundaryVisual.GetBounds(group, 12));
    }

    [Fact]
    public void SplitterFollowsTheResizedEdge()
    {
        DesktopSharedBoundaryMember[] group = [new(1, new(0, 0, 500, 800), DesktopResizeEdge.Right), new(2, new(500, 0, 500, 800), DesktopResizeEdge.Left)];
        Assert.Equal(new(544, 0, 12, 800), DesktopSharedBoundaryVisual.GetBounds(group, 12, 50));
    }

    [Fact]
    public void OverlayCounterScalingKeepsTheStripTwelveDipsWide()
    {
        DesktopSharedBoundaryMember[] group = [new(1, new(0, 0, 500, 800), DesktopResizeEdge.Right), new(2, new(500, 0, 500, 800), DesktopResizeEdge.Left)];
        DesktopSnapPlacement bounds = DesktopSharedBoundaryVisual.GetBounds(group, 12 / 0.38);
        Assert.Equal(12, bounds.Width * 0.38, 6);
        Assert.Equal(304, bounds.Height * 0.38, 6);
        Assert.Equal(500, bounds.CanvasX + bounds.Width / 2, 6);
    }

    [Fact]
    public void NegativeMonitorCoordinatesAndHighDpiArePreserved()
    {
        DesktopSharedBoundaryMember[] group = [new(1, new(-1920, -100, 960, 1000), DesktopResizeEdge.Right), new(2, new(-960, -100, 960, 1000), DesktopResizeEdge.Left)];
        Assert.Equal(new(-969, -100, 18, 1000), DesktopSharedBoundaryVisual.GetBounds(group, 18));
    }

    [Fact]
    public void NoStripIsCreatedWithoutASharedBoundary() => Assert.Equal(default, DesktopSharedBoundaryVisual.GetBounds([], 12));
}
