using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public sealed class StickyWindowController(IWindowStore store,
    IScroller scroller,
    ILogger<StickyWindowController> logger) :
    IStickyWindowController
{
    public bool IsSticky(IntPtr windowHandle) =>
        store.TryGet(windowHandle, out TrackedWindow? trackedWindow) && trackedWindow.IsSticky;

    public bool Pin(IntPtr windowHandle)
    {
        if (!store.TryGet(windowHandle, out TrackedWindow? trackedWindow))
        {
            return false;
        }

        if (trackedWindow.IsSticky)
        {
            return true;
        }

        if (!TryGetVisualOffset(out int visualOffset))
        {
            return false;
        }

        long viewportX = (long)trackedWindow.CanvasX - visualOffset;

        if (viewportX is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        trackedWindow.StickyViewportX = (int)viewportX;
        trackedWindow.IsSticky = true;
        store.NotifyChanged(windowHandle);
        scroller.Reposition();

        logger.LogInformation("Window {Handle} pinned to all pages", windowHandle);
        return true;
    }

    public bool Unpin(IntPtr windowHandle)
    {
        if (!store.TryGet(windowHandle, out TrackedWindow? trackedWindow))
        {
            return false;
        }

        if (!trackedWindow.IsSticky)
        {
            return true;
        }

        if (!TryGetVisualOffset(out int visualOffset))
        {
            return false;
        }

        long canvasX = (long)visualOffset + trackedWindow.StickyViewportX;

        if (canvasX is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        trackedWindow.CanvasX = (int)canvasX;
        trackedWindow.IsSticky = false;
        trackedWindow.StickyViewportX = 0;
        store.NotifyChanged(windowHandle);
        scroller.Reposition();

        logger.LogInformation("Window {Handle} unpinned on the current page", windowHandle);
        return true;
    }

    private bool TryGetVisualOffset(out int visualOffset)
    {
        double offset = Math.Round(scroller.VisualOffset);

        if (!double.IsFinite(offset) || offset is < int.MinValue or > int.MaxValue)
        {
            visualOffset = 0;
            return false;
        }

        visualOffset = (int)offset;
        return true;
    }
}