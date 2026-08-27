using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopSnapSlotOccupancyResolver
{
    private const double GeometryTolerance = 2;

    public bool IsOccupied(DesktopSnapPlacement placement, nint excludedWindow, IEnumerable<TrackedWindow> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        return windows.Any(window =>
            window.Handle != excludedWindow &&
            IsClose(window.CanvasX, placement.CanvasX) &&
            IsClose(window.CanvasY, placement.CanvasY) &&
            IsClose(window.Width, placement.Width) &&
            IsClose(window.Height, placement.Height));
    }

    private static bool IsClose(double actual, double expected) => Math.Abs(actual - expected) <= GeometryTolerance;
}
