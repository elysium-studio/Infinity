using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowPlacementCoordinator(IWindowStore windowStore,
    IScroller scroller,
    IWorkspace workspace,
    IWindowResizeSynchronizer resizeSynchronizer,
    IWindowCloser windowCloser,
    IWindowStateController windowStateController,
    IWindowPageTransitionGuard pageTransitionGuard,
    DesktopSnapPlacementResolver snapPlacementResolver,
    DesktopSnapSlotOccupancyResolver occupancyResolver)
{
    public bool TrySwapIntoSlot(nint movingHandle, nint occupantHandle, DesktopSnapPlacement targetPlacement)
    {
        if (!windowStore.TryGet(movingHandle, out TrackedWindow? movingWindow) ||
            !windowStore.TryGet(occupantHandle, out TrackedWindow? occupant) ||
            movingHandle == occupantHandle)
        {
            return false;
        }

        DesktopSnapPlacement sourcePlacement = GetPlacement(movingWindow);
        ApplyPlacements([(occupant, sourcePlacement), (movingWindow, targetPlacement)]);
        return true;
    }

    public bool TrySwap(nint firstHandle, nint secondHandle)
    {
        if (!windowStore.TryGet(firstHandle, out TrackedWindow? first) ||
            !windowStore.TryGet(secondHandle, out TrackedWindow? second) ||
            firstHandle == secondHandle)
        {
            return false;
        }

        DesktopSnapPlacement firstPlacement = GetPlacement(first);
        DesktopSnapPlacement secondPlacement = GetPlacement(second);
        ApplyPlacements([(first, secondPlacement), (second, firstPlacement)]);
        return true;
    }

    public bool TryMoveToSlot(nint windowHandle, int page, DesktopSnapLayoutKind layout, int slot, int screenOriginX, int screenOriginY)
    {
        if (!windowStore.TryGet(windowHandle, out TrackedWindow? window) ||
            !snapPlacementResolver.TryResolve(page, layout, slot, screenOriginX, screenOriginY, out DesktopSnapPlacement placement))
        {
            return false;
        }

        if (occupancyResolver.TryGetOccupant(placement, windowHandle, windowStore, out TrackedWindow? occupant) && occupant is not null)
        {
            return TrySwapIntoSlot(windowHandle, occupant.Handle, placement);
        }

        ApplyPlacements([(window, placement)]);
        return true;
    }

    public bool TryMoveToPage(nint windowHandle, int targetPage, bool center)
    {
        if (!windowStore.TryGet(windowHandle, out TrackedWindow? window) || targetPage < 0 || workspace.Width <= 0 || workspace.Height <= 0)
        {
            return false;
        }

        double pageLeft = workspace.WorkAreaX + (targetPage * (double)workspace.Width);
        double x;
        double y;

        if (center)
        {
            x = pageLeft + Math.Max(0, (workspace.Width - window.Width) / 2d);
            y = workspace.WorkAreaY + Math.Max(0, (workspace.Height - window.Height) / 2d);
        }
        else
        {
            int currentPage = GetPage(window);
            double currentPageLeft = workspace.WorkAreaX + (currentPage * (double)workspace.Width);
            double relativeX = window.CanvasX - currentPageLeft;
            x = pageLeft + Math.Clamp(relativeX, 0, Math.Max(0, workspace.Width - window.Width));
            y = Math.Clamp(window.CanvasY, workspace.WorkAreaY, workspace.WorkAreaY + Math.Max(0, workspace.Height - window.Height));
        }

        ApplyPlacements([(window, new DesktopSnapPlacement(x, y, window.Width, window.Height))]);
        return true;
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

            placements.Add((window, new DesktopSnapPlacement(
                window.CanvasX + (pageDelta * (double)workspace.Width),
                window.CanvasY,
                window.Width,
                window.Height)));
        }

        ApplyPlacements(placements);
        return placements.Count;
    }

    public bool TryClose(nint windowHandle) => windowCloser.TryClose(windowHandle);

    public WindowCommandState GetWindowCommandState(nint windowHandle) => windowStateController.GetState(windowHandle);

    public bool TryMaximize(nint windowHandle) => TryChangeWindowState(windowHandle, windowStateController.TryMaximize);

    public bool TryRestore(nint windowHandle) => TryChangeWindowState(windowHandle, windowStateController.TryRestore);

    public bool TryMinimize(nint windowHandle) => windowStateController.TryMinimize(windowHandle);

    public int GetPage(TrackedWindow window)
    {
        if (workspace.Width <= 0)
        {
            return 0;
        }

        double center = window.CanvasX - workspace.WorkAreaX + (window.Width / 2d);
        return Math.Max(0, (int)Math.Clamp(Math.Floor(center / workspace.Width), 0, int.MaxValue));
    }

    public void ApplyPlacements(IEnumerable<(TrackedWindow Window, DesktopSnapPlacement Placement)> placements)
    {
        (TrackedWindow Window, DesktopSnapPlacement Placement)[] materialized = [.. placements];

        foreach ((TrackedWindow window, DesktopSnapPlacement placement) in materialized)
        {
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

        foreach ((TrackedWindow window, _) in materialized)
        {
            windowStore.NotifyChanged(window.Handle);
        }
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
