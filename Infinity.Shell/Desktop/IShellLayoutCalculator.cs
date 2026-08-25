using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public interface IShellLayoutCalculator
{
    ShellWindowLayout Calculate(TrackedWindow trackedWindow, double panOffset, int monitorOffsetX, int monitorOffsetY, double scaleFactor, int screenWidth, int screenHeight);
}