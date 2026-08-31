using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infinity.Shell;

public sealed class PageNavigationPublisher(IDispatcher dispatcher, IPager pager, IInfinityGlanceBridge glanceBridge, PageTitleStore pageTitleStore, ILogger<PageNavigationPublisher> logger)
{
    private bool started;

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        pager.PageChanged += HandlePageChanged;
        pageTitleStore.TitleChanged += HandlePageTitleChanged;
        glanceBridge.MessageReceived += HandleGlanceMessageReceived;
        PublishPageNavigation();
    }

    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        pager.PageChanged -= HandlePageChanged;
        pageTitleStore.TitleChanged -= HandlePageTitleChanged;
        glanceBridge.MessageReceived -= HandleGlanceMessageReceived;
    }

    private void HandlePageChanged(int page) => dispatcher.Dispatch(() => PublishPageNavigation(page));

    private async void HandleGlanceMessageReceived(object? sender, InfinityGlanceMessageReceivedEventArgs args)
    {
        if (!string.Equals(args.Capability, InfinityGlanceTopics.PagesCapability, StringComparison.OrdinalIgnoreCase) || !string.Equals(args.Topic, InfinityGlanceTopics.PageTitleUpdate, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            InfinityPageTitleUpdate? update = JsonSerializer.Deserialize(args.Payload, InfinityGlanceJsonContext.Default.InfinityPageTitleUpdate);

            if (update is null || update.PageIndex < 0 || update.PageIndex >= pager.PageCount)
            {
                return;
            }

            await pageTitleStore.UpdateAsync(update.PageIndex, update.PageTitle);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update the Infinity page title from Glance");
        }
    }

    private void HandlePageTitleChanged(int page, string title) => dispatcher.Dispatch(() =>
    {
        if (pager.CurrentPage == page)
        {
            PublishPageNavigation(page, title);
        }
    });

    private void PublishPageNavigation() => PublishPageNavigation(pager.CurrentPage);

    private void PublishPageNavigation(int page) => PublishPageNavigation(page, pageTitleStore.GetTitle(page));

    private void PublishPageNavigation(int page, string title) => glanceBridge.PublishPageNavigation(new InfinityPageNavigationState(page, page + 1, title));
}
