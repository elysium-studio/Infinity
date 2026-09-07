using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowDragAnchorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void PreservesTheGrabbedTitleBarPoint(double horizontalRatio)
    {
        Assert.True(DesktopWindowDragAnchor.TryCreate(100 + 1200 * horizontalRatio, 215, new(100, 200, 1200, 800), out DesktopWindowDragAnchor anchor));
        (double x, double y) = anchor.GetOffset(1200, 800);
        Assert.Equal(1200 * horizontalRatio, x, 6);
        Assert.Equal(15, y, 6);
    }

    [Fact]
    public void UsesVisibleBoundsRatherThanInvisibleResizeBorders()
    {
        Assert.True(DesktopWindowDragAnchor.TryCreate(408, 118, new(108, 108, 1000, 700), out DesktopWindowDragAnchor anchor));
        Assert.Equal((300d, 10d), anchor.GetOffset(1000, 700));
    }

    [Fact]
    public void NegativeMonitorOriginsDoNotChangeTheGrabPoint()
    {
        Assert.True(DesktopWindowDragAnchor.TryCreate(-1500, -480, new(-1800, -500, 1000, 700), out DesktopWindowDragAnchor anchor));
        Assert.Equal((300d, 20d), anchor.GetOffset(1000, 700));
    }

    [Fact]
    public void KeepsTheSameRelativePointWhenCaptureDimensionsChange()
    {
        Assert.True(DesktopWindowDragAnchor.TryCreate(400, 120, new(100, 100, 1200, 800), out DesktopWindowDragAnchor anchor));
        Assert.Equal((450d, 30d), anchor.GetOffset(1800, 1200));
    }

    [Theory]
    [InlineData(-5, -5, 0, 0)]
    [InlineData(1005, 705, 1000, 700)]
    public void GrabbingAnOuterResizeBorderClampsToTheVisibleEdge(double pointerX, double pointerY, double expectedX, double expectedY)
    {
        Assert.True(DesktopWindowDragAnchor.TryCreate(pointerX, pointerY, new(0, 0, 1000, 700), out DesktopWindowDragAnchor anchor));
        Assert.Equal((expectedX, expectedY), anchor.GetOffset(1000, 700));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidWindowDimensions(double width)
    {
        Assert.False(DesktopWindowDragAnchor.TryCreate(10, 10, new(0, 0, width, 700), out _));
    }
}
