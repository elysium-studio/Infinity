using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class ShellLayoutCalculator :
    IShellLayoutCalculator
{
    public ShellWindowLayout Calculate(TrackedWindow trackedWindow, double panOffset, int monitorOffsetX, int monitorOffsetY, double scaleFactor, int screenWidth, int screenHeight)
    {
        double x = (trackedWindow.CanvasX - panOffset - monitorOffsetX) * scaleFactor;
        double y = (trackedWindow.CanvasY - monitorOffsetY) * scaleFactor;
        double width = Math.Max(2, trackedWindow.Width * scaleFactor);
        double height = Math.Max(2, trackedWindow.Height * scaleFactor);
        return new ShellWindowLayout(x, y, width, height);
    }
}
