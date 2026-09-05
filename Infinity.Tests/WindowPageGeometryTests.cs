using Infinity.Application;
using Infinity.Application.Abstractions;

namespace Infinity.Tests;

public sealed class WindowPageGeometryTests
{
    private readonly WindowPageGeometry geometry = new();

    [Fact]
    public void PageUsesTheWindowLeftEdge()
    {
        TrackedWindow window = CreateWindow(1250, 400);
        Assert.Equal(1, geometry.GetPage(window, 1000));
    }


    [Fact]
    public void CenterPageUsesTheWindowCenter()
    {
        TrackedWindow window = CreateWindow(800, 500);
        Assert.Equal(1, geometry.GetCenterPage(window, 1000));
    }


    [Fact]
    public void MeaningfulVisibilityRequiresSixtyPercentOfTheWindow()
    {
        TrackedWindow window = CreateWindow(800, 500);
        Assert.False(geometry.IsMeaningfullyVisible(window, 0, 1000));
        Assert.True(geometry.IsMeaningfullyVisible(window, 100, 1000));
    }


    [Fact]
    public void TargetOffsetDoesNotMoveBeforeTheOwningPage()
    {
        TrackedWindow window = CreateWindow(1000, 200);
        Assert.Equal(1000, geometry.GetTargetOffset(window, 1000, 1));
    }


    private static TrackedWindow CreateWindow(int canvasX, int width) => new()
    {
        Handle = new(1),
        CanvasX = canvasX,
        CanvasY = 0,
        Width = width,
        Height = 500
    };
}
