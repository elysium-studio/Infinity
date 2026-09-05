using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopPageArrangementCoordinator(IWindowStore windowStore,
    DesktopSnapLayoutCatalog layoutCatalog,
    DesktopSnapPlacementResolver placementResolver,
    DesktopSnapSlotOccupancyResolver occupancyResolver,
    DesktopWindowPlacementCoordinator windowPlacementCoordinator)
{
    public int Arrange(int page, DesktopSnapLayoutKind layout, int screenOriginX, int screenOriginY)
    {
        DesktopSnapLayoutDefinition? definition = layoutCatalog.Get(layout);

        if (page < 0 || definition is null)
        {
            return 0;
        }

        TrackedWindow[] pageWindows = [.. windowStore
            .Where(window => windowPlacementCoordinator.GetPage(window) == page)
            .OrderBy(window => window.CanvasY)
            .ThenBy(window => window.CanvasX)
            .ThenBy(window => (long)window.Handle)];
        List<DesktopSnapPlacement> emptySlots = [];
        HashSet<nint> placedHandles = [];

        for (int slot = 0; slot < definition.Slots.Count; slot++)
        {
            if (!placementResolver.TryResolve(page, layout, slot, screenOriginX, screenOriginY, out DesktopSnapPlacement placement))
            {
                continue;
            }

            if (occupancyResolver.TryGetOccupant(placement, 0, pageWindows, out TrackedWindow? occupant) && occupant is not null)
            {
                placedHandles.Add(occupant.Handle);
            }
            else
            {
                emptySlots.Add(placement);
            }
        }

        TrackedWindow[] windowsToPlace = [.. pageWindows.Where(window => !placedHandles.Contains(window.Handle)).Take(emptySlots.Count)];
        return windowPlacementCoordinator.ApplyPlacements(windowsToPlace.Select((window, index) => (window, emptySlots[index])), animate: true)
            ? windowsToPlace.Length : 0;
    }
}
