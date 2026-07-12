using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class WindowPageMover(IWindowStore store,
    IScroller scroller,
    IPager pager,
    Func<int> workspaceWidth,
    ILogger<WindowPageMover> logger) :
    IWindowPageMover
{
    public bool TryGetPage(IntPtr windowHandle, out int page)
    {
        int width = workspaceWidth();

        if (width <= 0 || !store.TryGet(windowHandle, out TrackedWindow? trackedWindow))
        {
            page = 0;
            return false;
        }

        page = (int)Math.Floor(trackedWindow.CanvasX / (double)width);
        return true;
    }

    public bool MoveToPage(IntPtr windowHandle, int targetPage)
    {
        if (targetPage < 0 || pager.MaxPages is int maxPages && targetPage >= maxPages)
        {
            return false;
        }

        if (!store.TryGet(windowHandle, out TrackedWindow? trackedWindow))
        {
            return false;
        }

        int width = workspaceWidth();

        if (width <= 0)
        {
            return false;
        }

        int currentPage = (int)Math.Floor(trackedWindow.CanvasX / (double)width);

        if (currentPage == targetPage)
        {
            return true;
        }

        long positionWithinPage = (long)trackedWindow.CanvasX - (long)currentPage * width;
        long targetCanvasX = (long)targetPage * width + positionWithinPage;

        if (targetCanvasX is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        trackedWindow.CanvasX = (int)targetCanvasX;
        store.NotifyChanged(windowHandle);
        scroller.Reposition();

        logger.LogInformation("Window {Handle} moved to page {Page}", windowHandle, targetPage);
        return true;
    }
}
