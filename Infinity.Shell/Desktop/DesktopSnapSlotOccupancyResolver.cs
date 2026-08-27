using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopSnapSlotOccupancyResolver
{
    private const double GeometryTolerance = 2;

    public bool IsOccupied(DesktopSnapPlacement placement, nint excludedWindow, IEnumerable<TrackedWindow> windows)
        => TryGetOccupant(placement, excludedWindow, windows, out _);

    public bool TryGetOccupant(DesktopSnapPlacement placement, nint excludedWindow, IEnumerable<TrackedWindow> windows, out TrackedWindow? occupant)
    {
        ArgumentNullException.ThrowIfNull(windows);

        occupant = windows.FirstOrDefault(window =>
            window.Handle != excludedWindow &&
            IsClose(window.CanvasX, placement.CanvasX) &&
            IsClose(window.CanvasY, placement.CanvasY) &&
            IsClose(window.Width, placement.Width) &&
            IsClose(window.Height, placement.Height));

        return occupant is not null;
    }

    private static bool IsClose(double actual, double expected) => Math.Abs(actual - expected) <= GeometryTolerance;
}
