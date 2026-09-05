using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowPlacementCoordinator(IWindowStore windowStore, IScroller scroller, IWorkspace workspace, IWindowResizeSynchronizer resizeSynchronizer, IWindowCloser windowCloser, IWindowStateController windowStateController, IWindowPageTransitionGuard pageTransitionGuard, DesktopSnapPlacementResolver snapPlacementResolver, DesktopSnapSlotOccupancyResolver occupancyResolver, DesktopWindowFrameGeometry frameGeometry)
{
    public event Action<IReadOnlyList<nint>>? PlacementStarting;

    public event Action<IReadOnlyList<nint>>? PlacementCompleted;

    public bool TrySwapIntoSlot(nint movingHandle, nint occupantHandle, DesktopSnapPlacement targetPlacement)
    {
        if (!windowStore.TryGet(movingHandle, out TrackedWindow? movingWindow) || !windowStore.TryGet(occupantHandle, out TrackedWindow? occupant) || movingHandle == occupantHandle)
        {
            return false;
        }

        DesktopSnapPlacement sourcePlacement = frameGeometry.GetVisiblePlacement(movingWindow);
        return ApplyPlacements([(occupant, sourcePlacement), (movingWindow, targetPlacement)], animate: true, visibleFrame: true);
    }


    public bool TrySwap(nint firstHandle, nint secondHandle)
    {
        if (!windowStore.TryGet(firstHandle, out TrackedWindow? first) || !windowStore.TryGet(secondHandle, out TrackedWindow? second) || firstHandle == secondHandle)
        {
            return false;
        }

        DesktopSnapPlacement firstPlacement = frameGeometry.GetVisiblePlacement(first);
        DesktopSnapPlacement secondPlacement = frameGeometry.GetVisiblePlacement(second);
        return ApplyPlacements([(first, secondPlacement), (second, firstPlacement)], animate: true, visibleFrame: true);
    }


    public bool TryMoveToSlot(nint windowHandle, int page, DesktopSnapLayoutKind layout, int slot, int screenOriginX, int screenOriginY)
    {
        if (!windowStore.TryGet(windowHandle, out TrackedWindow? window) || !snapPlacementResolver.TryResolve(page, layout, slot, screenOriginX, screenOriginY, out DesktopSnapPlacement placement))
        {
            return false;
        }

        if (occupancyResolver.TryGetOccupant(placement, windowHandle, windowStore, out TrackedWindow? occupant) && occupant is not null)
        {
            return TrySwapIntoSlot(windowHandle, occupant.Handle, placement);
        }

        return TryPlaceInSlot(windowHandle, placement);
    }


    public bool TryPlaceInSlot(nint windowHandle, DesktopSnapPlacement placement) => windowStore.TryGet(windowHandle, out TrackedWindow? window) && ApplyPlacements([(window, placement)], animate: true, visibleFrame: true);

    public bool TryMoveToPage(nint windowHandle, int targetPage, bool center)
    {
        if (!windowStore.TryGet(windowHandle, out TrackedWindow? window) || targetPage < 0 || workspace.Width <= 0 || workspace.Height <= 0)
        {
            return false;
        }

        double pageLeft = workspace.WorkAreaX + (targetPage * (double)workspace.Width);
        if (!TryPrepareForMove(windowHandle, out _))
        {
            return false;
        }

        DesktopSnapPlacement visible = frameGeometry.GetVisiblePlacement(window);
        double x;
        double y;
        if (center)
        {
            x = pageLeft + Math.Max(0, (workspace.Width - visible.Width) / 2d);
            y = workspace.WorkAreaY + Math.Max(0, (workspace.Height - visible.Height) / 2d);
        }
        else
        {
            int currentPage = GetPage(window);
            double currentPageLeft = workspace.WorkAreaX + (currentPage * (double)workspace.Width);
            double relativeX = visible.CanvasX - currentPageLeft;
            x = pageLeft + Math.Clamp(relativeX, 0, Math.Max(0, workspace.Width - visible.Width));
            y = Math.Clamp(visible.CanvasY, workspace.WorkAreaY, workspace.WorkAreaY + Math.Max(0, workspace.Height - visible.Height));
        }

        x += window.CanvasX - visible.CanvasX;
        y += window.CanvasY - visible.CanvasY;
        return ApplyPlacements([(window, new DesktopSnapPlacement(x, y, window.Width, window.Height))], animate: true);
    }


    public int MoveByPages(IEnumerable<nint> windowHandles, int pageDelta, int? maximumPageCount)
    {
        if (pageDelta == 0 || workspace.Width <= 0)
        {
            return 0;
        }

        List<(TrackedWindow Window, DesktopSnapPlacement Placement)> placements = [];
        foreach (nint handle in windowHandles.Distinct())
        {
            if (!windowStore.TryGet(handle, out TrackedWindow? window))
            {
                continue;
            }

            int targetPage = GetPage(window) + pageDelta;
            if (targetPage < 0 || maximumPageCount.HasValue && targetPage >= maximumPageCount.Value)
            {
                continue;
            }

            if (!TryPrepareForMove(handle, out _))
            {
                continue;
            }

            placements.Add((window, new DesktopSnapPlacement(window.CanvasX + (pageDelta * (double)workspace.Width), window.CanvasY, window.Width, window.Height)));
        }

        return ApplyPlacements(placements, animate: true) ? placements.Count : 0;
    }


    public bool TryClose(nint windowHandle) => windowCloser.TryClose(windowHandle);

    public WindowCommandState GetWindowCommandState(nint windowHandle) => windowStateController.GetState(windowHandle);

    public bool TryMaximize(nint windowHandle) => TryChangeWindowState(windowHandle, windowStateController.TryMaximize);

    public bool TryRestore(nint windowHandle) => TryChangeWindowState(windowHandle, windowStateController.TryRestore);

    public bool TryMinimize(nint windowHandle) => windowStateController.TryMinimize(windowHandle);

    public bool TryPrepareForMove(nint windowHandle, out DesktopSnapPlacement placement) => TryPrepareForMove(windowHandle, out placement, out _);

    public bool TryPrepareForMove(nint windowHandle, out DesktopSnapPlacement placement, out DesktopSnapPlacement originalPlacement)
    {
        placement = default;
        originalPlacement = default;
        if (!windowStore.TryGet(windowHandle, out TrackedWindow? window))
        {
            return false;
        }

        originalPlacement = placement = GetPlacement(window);
        if (!windowStateController.GetState(windowHandle).CanRestore)
        {
            return true;
        }

        if (workspace.Width <= 0 || workspace.Height <= 0)
        {
            return false;
        }

        int page = GetPage(window);
        pageTransitionGuard.PreservePage(windowHandle, page, workspace.Width, workspace.WorkAreaX);
        if (!windowStateController.TryRestoreForMove(windowHandle, out WindowRestoreBounds bounds))
        {
            pageTransitionGuard.Clear(windowHandle);
            return false;
        }

        double pageLeft = workspace.WorkAreaX + page * (double)workspace.Width;
        double relativeX = Math.Clamp(bounds.X - (double)workspace.WorkAreaX, 0, Math.Max(0, workspace.Width - bounds.Width));
        window.CanvasX = Round(pageLeft + relativeX);
        window.CanvasY = Round(Math.Clamp(bounds.Y, workspace.WorkAreaY, workspace.WorkAreaY + Math.Max(0, workspace.Height - bounds.Height)));
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        window.InvalidatePlacement();
        placement = GetPlacement(window);
        scroller.Reposition();
        windowStore.NotifyChanged(windowHandle);
        return true;
    }


    public void CompleteMove(nint windowHandle) => pageTransitionGuard.Clear(windowHandle);

    public int GetPage(TrackedWindow window)
    {
        if (workspace.Width <= 0)
        {
            return 0;
        }

        double center = window.CanvasX - workspace.WorkAreaX + (window.Width / 2d);
        return Math.Max(0, (int)Math.Clamp(Math.Floor(center / workspace.Width), 0, int.MaxValue));
    }


    public bool ApplyPlacements(IEnumerable<(TrackedWindow Window, DesktopSnapPlacement Placement)> placements, bool animate = false, bool visibleFrame = false)
    {
        (TrackedWindow Window, DesktopSnapPlacement Placement)[] materialized = [..placements];
        if (!animate || materialized.Length == 0)
        {
            return ApplyPlacementsCore(materialized, visibleFrame);
        }

        nint[] handles = [..materialized.Select(item => item.Window.Handle).Distinct()];
        PlacementStarting?.Invoke(handles);
        try
        {
            return ApplyPlacementsCore(materialized, visibleFrame);
        }
        finally
        {
            PlacementCompleted?.Invoke(handles);
        }
    }


    private bool ApplyPlacementsCore((TrackedWindow Window, DesktopSnapPlacement Placement)[] materialized, bool visibleFrame)
    {
        foreach ((TrackedWindow window, _)in materialized)
        {
            if (!TryPrepareForMove(window.Handle, out _))
            {
                return false;
            }
        }

        foreach ((TrackedWindow window, DesktopSnapPlacement target)in materialized)
        {
            DesktopSnapPlacement placement = visibleFrame ? frameGeometry.ToOuter(window.Handle, target) : target;
            if (workspace.Width > 0)
            {
                int targetPage = Math.Max(0, (int)Math.Floor((placement.CanvasX - workspace.WorkAreaX + placement.Width / 2) / workspace.Width));
                pageTransitionGuard.PreservePage(window.Handle, targetPage, workspace.Width, workspace.WorkAreaX);
            }

            int width = RoundPositive(placement.Width);
            int height = RoundPositive(placement.Height);
            if (window.Width != width || window.Height != height)
            {
                resizeSynchronizer.TrySynchronize(window.Handle, width, height);
            }

            window.CanvasX = Round(placement.CanvasX);
            window.CanvasY = Round(placement.CanvasY);
            window.Width = width;
            window.Height = height;
            window.InvalidatePlacement();
        }

        if (materialized.Length > 0)
        {
            scroller.Reposition();
        }

        foreach ((TrackedWindow window, _)in materialized)
        {
            windowStore.NotifyChanged(window.Handle);
        }

        return true;
    }


    private static DesktopSnapPlacement GetPlacement(TrackedWindow window) => new(window.CanvasX, window.CanvasY, window.Width, window.Height);

    private bool TryChangeWindowState(nint windowHandle, Func<nint, bool> changeState)
    {
        if (!windowStore.TryGet(windowHandle, out TrackedWindow? window))
        {
            return false;
        }

        pageTransitionGuard.PreservePage(windowHandle, GetPage(window), workspace.Width, workspace.WorkAreaX);
        if (changeState(windowHandle))
        {
            return true;
        }

        pageTransitionGuard.Clear(windowHandle);
        return false;
    }


    private static int Round(double value) => (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);

    private static int RoundPositive(double value) => Math.Max(1, Round(value));
}
