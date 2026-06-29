using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public class ShellLayoutCalculator(IWorkspace workspace) :
    IShellLayoutCalculator
{
    public ShellWindowLayout Calculate(TrackedWindow trackedWindow, double panOffset, int monitorOffsetX, double scaleFactor, int screenWidth, int screenHeight)
    {
        double x = (trackedWindow.CanvasX - panOffset - monitorOffsetX) * scaleFactor;
        double y = (trackedWindow.CanvasY - workspace.WorkAreaY) * scaleFactor;
        double width = Math.Max(2, trackedWindow.Width * scaleFactor);
        double height = Math.Max(2, trackedWindow.Height * scaleFactor);
        return new ShellWindowLayout(x, y, width, height);
    }
}