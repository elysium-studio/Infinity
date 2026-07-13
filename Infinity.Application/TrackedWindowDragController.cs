using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class TrackedWindowDragController(IWindowStore store,
    IScroller scroller,
    ILogger<TrackedWindowDragController> logger) :
    ITrackedWindowDragController
{
    private readonly Lock syncRoot = new();
    private DragSession? session;

    public IntPtr DraggingWindow
    {
        get
        {
            lock (syncRoot)
            {
                return session?.WindowHandle ?? IntPtr.Zero;
            }
        }
    }

    public bool Begin(IntPtr windowHandle)
    {
        if (!store.TryGet(windowHandle, out TrackedWindow? trackedWindow) ||
            !TryRound(scroller.VisualOffset, out int visualOffset))
        {
            return false;
        }

        long viewportX = trackedWindow.IsSticky
            ? trackedWindow.StickyViewportX
            : (long)trackedWindow.CanvasX - visualOffset;

        if (viewportX is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        lock (syncRoot)
        {
            if (session is not null)
            {
                return false;
            }

            session = new(windowHandle, (int)viewportX, trackedWindow.CanvasY);
        }

        scroller.Reset();
        logger.LogDebug("Tracked window drag started for {Handle}", windowHandle);
        return true;
    }

    public bool Move(IntPtr windowHandle, double horizontalDelta, double verticalDelta)
    {
        DragSession dragSession;

        lock (syncRoot)
        {
            if (session is not DragSession currentSession || currentSession.WindowHandle != windowHandle)
            {
                return false;
            }

            dragSession = currentSession;
        }

        if (!store.TryGet(windowHandle, out TrackedWindow? trackedWindow))
        {
            End(windowHandle);
            return false;
        }

        if (!TryRound(scroller.VisualOffset, out int visualOffset) ||
            !TryRound(horizontalDelta, out int roundedHorizontalDelta) ||
            !TryRound(verticalDelta, out int roundedVerticalDelta))
        {
            return false;
        }

        long viewportX = (long)dragSession.ViewportX + roundedHorizontalDelta;
        long canvasX = visualOffset + viewportX;
        long canvasY = (long)dragSession.CanvasY + roundedVerticalDelta;

        if (viewportX is < int.MinValue or > int.MaxValue ||
            canvasX is < int.MinValue or > int.MaxValue ||
            canvasY is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        trackedWindow.CanvasX = (int)canvasX;
        trackedWindow.CanvasY = (int)canvasY;

        if (trackedWindow.IsSticky)
        {
            trackedWindow.StickyViewportX = (int)viewportX;
        }

        scroller.Reposition();
        return true;
    }

    public void End(IntPtr windowHandle)
    {
        bool ended;

        lock (syncRoot)
        {
            ended = session is DragSession currentSession && currentSession.WindowHandle == windowHandle;

            if (ended)
            {
                session = null;
            }
        }

        if (!ended)
        {
            return;
        }

        store.NotifyChanged(windowHandle);
        logger.LogDebug("Tracked window drag ended for {Handle}", windowHandle);
    }

    private static bool TryRound(double value, out int roundedValue)
    {
        double rounded = Math.Round(value);

        if (!double.IsFinite(rounded) || rounded is < int.MinValue or > int.MaxValue)
        {
            roundedValue = 0;
            return false;
        }

        roundedValue = (int)rounded;
        return true;
    }

    private readonly record struct DragSession(IntPtr WindowHandle,
        int ViewportX,
        int CanvasY);
}
