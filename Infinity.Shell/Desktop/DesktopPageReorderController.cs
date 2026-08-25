using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell;

public sealed class DesktopPageReorderController(IWindowStore windowStore, IScroller scroller, IWorkspace workspace, PageTitleStore pageTitleStore, ILogger<DesktopPageReorderController> logger)
{
    public async Task<IReadOnlyDictionary<int, string>> ReorderAsync(int sourcePage, int targetPage)
    {
        if (sourcePage == targetPage || workspace.Width <= 0)
        {
            return new Dictionary<int, string>();
        }

        IReadOnlyDictionary<int, string> reorderedTitles = await pageTitleStore.ReorderAsync(sourcePage, targetPage);

        int workspaceWidth = workspace.Width;
        List<TrackedWindow> changedWindows = [];

        foreach (TrackedWindow window in windowStore)
        {
            int page = PageReorderMapping.GetPage(window, workspaceWidth);
            int reorderedPage = PageReorderMapping.Map(page, sourcePage, targetPage);

            if (reorderedPage == page)
            {
                continue;
            }

            long reorderedX = (long)window.CanvasX + ((long)reorderedPage - page) * workspaceWidth;

            if (reorderedX is < int.MinValue or > int.MaxValue)
            {
                logger.LogWarning("Skipped page reorder for window {WindowHandle} because its target position is outside the supported range", window.Handle);
                continue;
            }

            window.CanvasX = (int)reorderedX;
            window.InvalidatePlacement();
            changedWindows.Add(window);
        }

        foreach (TrackedWindow window in changedWindows)
        {
            windowStore.NotifyChanged(window.Handle);
        }

        scroller.Reposition();
        logger.LogInformation("Reordered desktop page {SourcePage} to {TargetPage} with {WindowCount} windows updated", sourcePage, targetPage, changedWindows.Count);

        return reorderedTitles;
    }
}
