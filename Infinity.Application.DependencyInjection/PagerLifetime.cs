using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infinity.Application.DependencyInjection;

public class PagerLifetime(IWindowTracker tracker,
    IWindowEnumerator enumerator,
    IWindowEventListener listener,
    IWindowDragGuard dragGuard,
    IWindowDragScroller dragScroller,
    IPageGestureSource gestureSource,
    IWindowPageJumper jumper,
    IWindowTitleSynchronizer titleSynchronizer,
    IWindowZOrder zOrder,
    IScrollInputSource scrollInput,
    IPointerInputSource pointerInput,
    IKeyboardInputSource keyboardInput,
    IScroller coordinator,
    IPanState state,
    IWindowStore repository,
    IWorkspace workspace,
    IPager pager,
    IWindowCollection windowCollection,
    IScrollTimer timer,
    ILogger<PagerLifetime> logger) :
    IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Pager lifetime initialising");

        timer.Tick += HandleScrollTimerTick;

        enumerator.EnumerateVisible(windowHandle => tracker.TryRegister(windowHandle));

        int minCanvasX = repository
            .Select(window => window.CanvasX)
            .DefaultIfEmpty(0)
            .Min();

        if (minCanvasX < 0)
        {
            int pageShift = (int)Math.Ceiling(Math.Abs((double)minCanvasX) / workspace.Width) * workspace.Width;
            logger.LogInformation("Negative canvas offset detected, shifting pages by {PageShift}px", pageShift);

            foreach (TrackedWindow trackedWindow in repository)
            {
                trackedWindow.CanvasX += pageShift;
            }

            state.SetOffset(pageShift);
        }

        zOrder.Refresh();
        listener.Start();
        tracker.Start();
        dragGuard.Start();
        dragScroller.Start();
        titleSynchronizer.Start();
        zOrder.Start();
        scrollInput.Start();
        coordinator.Start();
        windowCollection.Start();
        pager.Start();
        gestureSource.Start();
        jumper.Start();

        logger.LogInformation("Pager lifetime initialised");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Pager lifetime shutting down");

        timer.Stop();
        timer.Tick -= HandleScrollTimerTick;

        pointerInput.Dispose();
        keyboardInput.Dispose();

        jumper.Stop();
        gestureSource.Stop();
        pager.Stop();
        windowCollection.Stop();
        tracker.Stop();
        dragGuard.Stop();
        dragScroller.Stop();
        titleSynchronizer.Stop();
        zOrder.Stop();
        scrollInput.Stop();
        coordinator.Dispose();
        listener.Dispose();

        logger.LogInformation("Pager lifetime shut down");

        return Task.CompletedTask;
    }

    private void HandleScrollTimerTick(object? sender, EventArgs args) =>
        coordinator.OnTick();
}