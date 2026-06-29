using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class Pager(IWindowStore repository,
    IPanState state,
    IScroller coordinator,
    IWorkspace workspace,
    IWindowTracker tracker,
    ILogger<Pager> logger) :
    IPager
{
    private int lastPage;

    private int? maxPages;

    public event Action<int>? PageChanged;

    public int CurrentPage => Math.Max(0, (int)Math.Round(state.Offset / workspace.Width));

    public int? MaxPages => maxPages;

    public int PageCount
    {
        get
        {
            int currentPage = CurrentPage;

            int pageCount = repository.Any()
                ? (int)Math.Ceiling((repository.Max(w => w.CanvasX + w.Width) - 1) / (double)workspace.Width)
                : 1;

            pageCount = Math.Max(1, pageCount);
            pageCount = Math.Max(pageCount, currentPage + 1);

            return maxPages.HasValue
                ? Math.Min(pageCount, maxPages.Value)
                : pageCount;
        }
    }

    public void NavigateToPage(int page)
    {
        int targetPage = maxPages.HasValue
            ? Math.Min(page, maxPages.Value - 1)
            : page;

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
        logger.LogInformation("Pager started");
        lastPage = CurrentPage;
        state.OffsetChanged += HandleOffsetChanged;
        tracker.WindowRestored += HandleWindowRestored;
    }

    public void Stop()
    {
        logger.LogInformation("Pager stopped");
        state.OffsetChanged -= HandleOffsetChanged;
        tracker.WindowRestored -= HandleWindowRestored;
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

    private void HandleWindowRestored(IntPtr windowHandle)
    {
        if (!repository.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            return;
        }

        int page = (int)Math.Floor((double)trackedWindow.CanvasX / workspace.Width);
        logger.LogInformation("Window restored ({Handle}), navigating to page {Page}", windowHandle, page);
        NavigateToPage(page);
    }
}