using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopSnapSlotOccupancyResolver
{
    private const double GeometryTolerance = 2;

    public bool IsOccupied(DesktopSnapPlacement placement, nint excludedWindow, IEnumerable<TrackedWindow> windows)
        => TryGetOccupant(placement, excludedWindow, windows, out _);

    public bool IsOccupied(DesktopSnapPlacement placement, IReadOnlySet<nint> excludedWindows, IEnumerable<TrackedWindow> windows)
        => TryGetOccupant(placement, excludedWindows, windows, out _);

    public bool TryGetOccupant(DesktopSnapPlacement placement, nint excludedWindow, IEnumerable<TrackedWindow> windows, out TrackedWindow? occupant)
    {
        ArgumentNullException.ThrowIfNull(windows);

        foreach (TrackedWindow window in windows)
        {
            if (window.Handle != excludedWindow && Matches(window, placement))
            {
                occupant = window;
                return true;
            }
        }
        occupant = null;
        return false;
    }

    public bool TryGetOccupant(DesktopSnapPlacement placement, IReadOnlySet<nint> excludedWindows, IEnumerable<TrackedWindow> windows, out TrackedWindow? occupant)
    {
        ArgumentNullException.ThrowIfNull(excludedWindows);
        ArgumentNullException.ThrowIfNull(windows);

        foreach (TrackedWindow window in windows)
        {
            if (!excludedWindows.Contains(window.Handle) && Matches(window, placement))
            {
                occupant = window;
                return true;
            }
        }
        occupant = null;
        return false;
    }

    private static bool Matches(TrackedWindow window, DesktopSnapPlacement placement) =>
        IsClose(window.CanvasX, placement.CanvasX) &&
        IsClose(window.CanvasY, placement.CanvasY) &&
        IsClose(window.Width, placement.Width) &&
        IsClose(window.Height, placement.Height);

    private static bool IsClose(double actual, double expected) => Math.Abs(actual - expected) <= GeometryTolerance;
}
