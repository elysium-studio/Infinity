using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public sealed class TrackedWindowDragController(IWindowStore store, IScroller scroller, IWindowResizeSynchronizer resizeSynchronizer, ILogger<TrackedWindowDragController> logger) : ITrackedWindowDragController
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
        if (!store.TryGet(windowHandle, out _))
        {
            return false;
        }

        lock (syncRoot)
        {
            if (session is not null)
            {
                return false;
            }

            session = new(windowHandle);
        }

        scroller.Reset();
        logger.LogDebug("Tracked window drag started for {Handle}", windowHandle);
        return true;
    }


    public bool MoveTo(IntPtr windowHandle, double canvasX, double canvasY)
    {
        if (!TryGetDragWindow(windowHandle, out TrackedWindow? trackedWindow) || trackedWindow is null || !TryRound(canvasX, out int roundedCanvasX) || !TryRound(canvasY, out int roundedCanvasY))
        {
            return false;
        }

        trackedWindow.CanvasX = roundedCanvasX;
        trackedWindow.CanvasY = roundedCanvasY;
        scroller.Reposition();
        return true;
    }


    public bool MoveAndResize(IntPtr windowHandle, double canvasX, double canvasY, double width, double height)
    {
        if (!TryGetDragWindow(windowHandle, out TrackedWindow? trackedWindow) || trackedWindow is null || !TryRound(canvasX, out int roundedCanvasX) || !TryRound(canvasY, out int roundedCanvasY) || !TryRound(width, out int roundedWidth) || !TryRound(height, out int roundedHeight) || roundedWidth <= 0 || roundedHeight <= 0)
        {
            return false;
        }

        resizeSynchronizer.TrySynchronize(windowHandle, roundedWidth, roundedHeight);
        trackedWindow.CanvasX = roundedCanvasX;
        trackedWindow.CanvasY = roundedCanvasY;
        trackedWindow.Width = roundedWidth;
        trackedWindow.Height = roundedHeight;
        trackedWindow.InvalidatePlacement();
        scroller.Reposition();
        return true;
    }


    private bool TryGetDragWindow(IntPtr windowHandle, out TrackedWindow? trackedWindow)
    {
        lock (syncRoot)
        {
            if (session is not DragSession currentSession || currentSession.WindowHandle != windowHandle)
            {
                trackedWindow = null;
                return false;
            }
        }

        if (!store.TryGet(windowHandle, out trackedWindow))
        {
            End(windowHandle);
            return false;
        }

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


    private readonly record struct DragSession(IntPtr WindowHandle);
}
