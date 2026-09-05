using Infinity.Application.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class ShellLayoutCalculatorTests
{
    [Fact]
    public void CalculateTransformsWorkspaceCoordinatesIntoScaledViewportCoordinates()
    {
        ShellLayoutCalculator calculator = new();
        TrackedWindow window = CreateWindow(canvasX: 1_500, canvasY: 200, width: 800, height: 600);
        ShellWindowLayout layout = calculator.Calculate(window, panOffset: 1_000, monitorOffsetX: 100, monitorOffsetY: 40, scaleFactor: 0.5, screenWidth: 1920, screenHeight: 1080);
        Assert.Equal(200, layout.X);
        Assert.Equal(80, layout.Y);
        Assert.Equal(400, layout.Width);
        Assert.Equal(300, layout.Height);
    }


    [Fact]
    public void CalculateKeepsVerySmallWindowsVisible()
    {
        ShellLayoutCalculator calculator = new();
        TrackedWindow window = CreateWindow(canvasX: 0, canvasY: 0, width: 1, height: 1);
        ShellWindowLayout layout = calculator.Calculate(window, panOffset: 0, monitorOffsetX: 0, monitorOffsetY: 0, scaleFactor: 0.1, screenWidth: 1920, screenHeight: 1080);
        Assert.Equal(2, layout.Width);
        Assert.Equal(2, layout.Height);
    }


    private static TrackedWindow CreateWindow(int canvasX, int canvasY, int width, int height) => new()
    {
        Handle = new(1),
        CanvasX = canvasX,
        CanvasY = canvasY,
        Width = width,
        Height = height
    };
}
