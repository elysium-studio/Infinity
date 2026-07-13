using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class Pager(IWindowStore repository,
    IPanState state,
    IScroller coordinator,
    IWorkspace workspace,
    ILogger<Pager> logger) :
    IPager
{
    private int lastPage;

    private int? maxPages;
    private bool isStarted;

    public event Action<int>? PageChanged;

    public int CurrentPage => workspace.Width > 0 ? Math.Max(0, (int)Math.Round(state.Offset / workspace.Width)) : 0;

    public int? MaxPages => maxPages;

    public int PageCount
    {
        get
        {
            int currentPage = CurrentPage;
            TrackedWindow? rightmostWindow = repository
                .Where(window => !window.IsSticky)
                .MaxBy(window => (long)window.CanvasX + window.Width);

            int pageCount = rightmostWindow is not null && workspace.Width > 0
                ? (int)Math.Ceiling(((long)rightmostWindow.CanvasX + rightmostWindow.Width - 1) / (double)workspace.Width)
                : 1;

            pageCount = Math.Max(1, pageCount);
            pageCount = Math.Max(pageCount, currentPage + 1);

            return maxPages.HasValue ? Math.Min(pageCount, maxPages.Value) : pageCount;
        }
    }

    public void NavigateToPage(int page)
    {
        int targetPage = maxPages.HasValue
            ? Math.Min(page, maxPages.Value - 1)
            : page;

        targetPage = Math.Max(0, targetPage);

        logger.LogInformation("Navigating to page {Page}", targetPage);

        double targetOffset = targetPage * workspace.Width;
        coordinator.ScrollTo(targetOffset);
    }

    public void SetMaxPages(int? maxPages)
    {
        logger.LogInformation("Max pages set to {MaxPages}", maxPages);
        this.maxPages = maxPages;
    }

    public void Start()
    {
        if (isStarted)
        {
            return;
        }

        isStarted = true;
        logger.LogInformation("Pager started");
        lastPage = CurrentPage;
        state.OffsetChanged += HandleOffsetChanged;
    }

    public void Stop()
    {
        if (!isStarted)
        {
            return;
        }

        isStarted = false;
        logger.LogInformation("Pager stopped");
        state.OffsetChanged -= HandleOffsetChanged;
    }

    private void HandleOffsetChanged()
    {
        int page = CurrentPage;

        if (page == lastPage)
        {
            return;
        }

        lastPage = page;
        logger.LogInformation("Page changed to {Page}", page);
        PageChanged?.Invoke(page);
    }
}
