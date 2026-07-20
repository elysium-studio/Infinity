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
    IWindowStack stack,
    IForegroundWindowTracker foreground,
    IScrollInputSource scrollInput,
    IPointerInputSource pointerInput,
    IKeyboardInputSource keyboardInput,
    IScroller coordinator,
    IWindowConcealmentRecovery concealmentRecovery,
    StartupPageRestorer startupPageRestorer,
    IPager pager,
    IWindowCollectionLifetime windowCollection,
    IScrollTimer timer,
    ILogger<PagerLifetime> logger) :
    IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Pager lifetime initialising");

        timer.Tick += HandleScrollTimerTick;

        concealmentRecovery.RecoverStrandedWindows();
        enumerator.EnumerateVisible(windowHandle => tracker.TryRegisterExisting(windowHandle));
        startupPageRestorer.Restore();

        titleSynchronizer.Start();
        stack.Refresh();
        listener.Start();
        tracker.Start();
        dragGuard.Start();
        dragScroller.Start();
        stack.Start();
        foreground.Start();
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
        stack.Stop();
        foreground.Stop();
        scrollInput.Stop();
        coordinator.Dispose();
        listener.Dispose();

        logger.LogInformation("Pager lifetime shut down");

        return Task.CompletedTask;
    }

    private void HandleScrollTimerTick(object? sender, EventArgs args) =>
        coordinator.OnTick();
}
