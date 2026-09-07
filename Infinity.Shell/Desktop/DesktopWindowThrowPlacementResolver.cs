using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowThrowPlacementResolver(IWindowStore windows, IWorkspace workspace, DesktopSnapLayoutCatalog catalog, DesktopSnapPlacementResolver placements, DesktopSnapSlotOccupancyResolver occupancy, DesktopWindowFrameGeometry frames)
{
    public DesktopSnapPlacement? GetOrigin(nint handle) => windows.TryGet(handle, out TrackedWindow? window) ? frames.GetVisiblePlacement(window) : null;

    public bool TryResolve(nint handle, int sourcePage, DesktopSnapPlacement origin, int targetPage, DesktopSnapLayoutKind layout, out DesktopSnapPlacement placement)
    {
        placement = default;
        if (sourcePage < 0 || targetPage < 0 || workspace.Width <= 0 || workspace.Height <= 0)
        {
            return false;
        }

        double relativeX = origin.CanvasX - workspace.WorkAreaX - sourcePage * (double)workspace.Width;
        double pageLeft = workspace.WorkAreaX + targetPage * (double)workspace.Width;
        DesktopSnapPlacement desired = new(pageLeft + relativeX, origin.CanvasY, origin.Width, origin.Height);
        if (layout == DesktopSnapLayoutKind.None)
        {
            placement = desired with
            {
                CanvasX = pageLeft + Math.Clamp(relativeX, 0, Math.Max(0, workspace.Width - origin.Width)),
                CanvasY = Math.Clamp(origin.CanvasY, workspace.WorkAreaY, workspace.WorkAreaY + Math.Max(0, workspace.Height - origin.Height))
            };
            return true;
        }

        DesktopSnapLayoutDefinition? definition = catalog.Get(layout);
        if (definition is null)
        {
            return false;
        }

        bool found = false;
        bool bestMatchingSize = false;
        double bestDistance = double.PositiveInfinity;
        for (int slot = 0; slot < definition.Slots.Count; slot++)
        {
            if (!placements.TryResolve(targetPage, layout, slot, workspace.WorkAreaX, workspace.WorkAreaY, out DesktopSnapPlacement candidate) || occupancy.IsOccupied(candidate, handle, windows))
            {
                continue;
            }

            bool matchingSize = Math.Abs(candidate.Width - origin.Width) <= 2 && Math.Abs(candidate.Height - origin.Height) <= 2;
            double dx = (candidate.CanvasX + candidate.Width / 2 - desired.CanvasX - desired.Width / 2) / workspace.Width;
            double dy = (candidate.CanvasY + candidate.Height / 2 - desired.CanvasY - desired.Height / 2) / workspace.Height;
            double distance = dx * dx + dy * dy;
            if (!found || matchingSize && !bestMatchingSize || matchingSize == bestMatchingSize && distance < bestDistance)
            {
                found = true;
                bestMatchingSize = matchingSize;
                bestDistance = distance;
                placement = candidate;
            }
        }

        return found;
    }
}
