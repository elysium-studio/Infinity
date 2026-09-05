using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public static class PageReorderMapping
{
    public static int GetPage(TrackedWindow window, double workspaceWidth)
    {
        if (workspaceWidth <= 0)
        {
            return 0;
        }

        double center = window.CanvasX + (window.Width / 2.0);
        return Math.Max(0, (int)Math.Clamp(Math.Floor(center / workspaceWidth), 0, int.MaxValue));
    }


    public static int Map(int page, int sourcePage, int targetPage)
    {
        if (page == sourcePage)
        {
            return targetPage;
        }

        if (sourcePage < targetPage && page > sourcePage && page <= targetPage)
        {
            return page - 1;
        }

        if (sourcePage > targetPage && page >= targetPage && page < sourcePage)
        {
            return page + 1;
        }

        return page;
    }
}
