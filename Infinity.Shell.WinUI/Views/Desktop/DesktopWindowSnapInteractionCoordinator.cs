using Infinity.Application.Abstractions;
using System.Collections.Generic;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowSnapInteractionCoordinator(
    DesktopOverviewConfiguration configuration,
    DesktopPageStrip pageStrip,
    DesktopSnapPlacementResolver placementResolver,
    DesktopSnapSlotOccupancyResolver occupancyResolver,
    DesktopWindowPreviewCollection previews,
    IWindowCollection windowCollection)
{
    private nint activeWindow;
    private bool isActive;
    private int monitorOriginX;
    private int monitorOriginY;
    private double pointerX;
    private double pointerY;

    public void Start(int originX, int originY)
    {
        monitorOriginX = originX;
        monitorOriginY = originY;

        if (isActive)
        {
            return;
        }

        isActive = true;
        previews.WindowDragMoved += HandleWindowDragMoved;
        previews.WindowDragCompleted += HandleWindowDragCompleted;
    }

    public void UpdateMonitorOrigin(int originX, int originY)
    {
        monitorOriginX = originX;
        monitorOriginY = originY;
    }

    public void Stop()
    {
        if (!isActive)
        {
            return;
        }

        previews.WindowDragMoved -= HandleWindowDragMoved;
        previews.WindowDragCompleted -= HandleWindowDragCompleted;
        isActive = false;
        Clear();
    }

    public void Refresh()
    {
        if (!isActive || !configuration.IsSnapAssistanceEnabled)
        {
            Clear();
            return;
        }

        if (activeWindow == 0 ||
            !pageStrip.TryUpdateWindowSnapTarget(pointerX, pointerY, out DesktopSnapSlotTarget target) ||
            !placementResolver.TryResolve(target.Page, target.Layout, target.Slot, monitorOriginX, monitorOriginY, out DesktopSnapPlacement placement))
        {
            ClearPreviewTarget();
            pageStrip.ClearWindowSnapTarget();
            return;
        }

        IReadOnlySet<nint> selectedHandles = previews.GetSelectedHandles();
        TrackedWindow? occupant;

        if (selectedHandles.Count > 1 && selectedHandles.Contains(activeWindow))
        {
            occupancyResolver.TryGetOccupant(placement, selectedHandles, windowCollection.AllTrackedWindows, out occupant);
        }
        else
        {
            occupancyResolver.TryGetOccupant(placement, activeWindow, windowCollection.AllTrackedWindows, out occupant);
        }

        previews.SetSnapTarget(activeWindow, new DesktopWindowSnapTarget(placement, occupant?.Handle ?? 0));
    }

    public void Clear()
    {
        ClearPreviewTarget();
        activeWindow = 0;
        pointerX = 0;
        pointerY = 0;
        pageStrip.ClearWindowSnapTarget();
    }

    private void HandleWindowDragMoved(nint handle, double x, double y)
    {
        activeWindow = handle;
        pointerX = x;
        pointerY = y;
        Refresh();
    }

    private void HandleWindowDragCompleted(nint handle)
    {
        if (activeWindow == handle)
        {
            Clear();
        }
    }

    private void ClearPreviewTarget()
    {
        if (activeWindow != 0)
        {
            previews.SetSnapTarget(activeWindow, null);
        }
    }
}
