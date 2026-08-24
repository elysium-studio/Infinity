using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infinity.Application.DependencyInjection;

public sealed class PagerLifetime(IWindowTracker tracker,
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
    IHostedService,
    IDisposable
{
    private int cleanupCompleted;
    private int startInitiated;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref startInitiated, 1) != 0)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation("Pager lifetime initialising");

        try
        {
            timer.Tick += HandleScrollTimerTick;

            cancellationToken.ThrowIfCancellationRequested();
            concealmentRecovery.RecoverStrandedWindows();
            enumerator.EnumerateVisible(windowHandle => tracker.TryRegisterExisting(windowHandle));
            startupPageRestorer.Restore();

            cancellationToken.ThrowIfCancellationRequested();
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
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Pager lifetime failed to initialise");
            Cleanup();
            throw;
        }

        logger.LogInformation("Pager lifetime initialised");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Pager lifetime shutting down");

        Cleanup();

        logger.LogInformation("Pager lifetime shut down");

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        if (Interlocked.Exchange(ref cleanupCompleted, 1) != 0)
        {
            return;
        }

        TryCleanup(timer.Stop, "scroll timer");
        TryCleanup(() => timer.Tick -= HandleScrollTimerTick, "scroll timer subscription");
        TryCleanup(pointerInput.Dispose, "pointer input");
        TryCleanup(keyboardInput.Dispose, "keyboard input");
        TryCleanup(jumper.Stop, "window page jumper");
        TryCleanup(gestureSource.Stop, "page gesture source");
        TryCleanup(pager.Stop, "pager");
        TryCleanup(windowCollection.Stop, "window collection");
        TryCleanup(tracker.Stop, "window tracker");
        TryCleanup(dragGuard.Stop, "window drag guard");
        TryCleanup(dragScroller.Stop, "window drag scroller");
        TryCleanup(titleSynchronizer.Stop, "window title synchronizer");
        TryCleanup(stack.Stop, "window stack");
        TryCleanup(foreground.Stop, "foreground window tracker");
        TryCleanup(scrollInput.Stop, "scroll input");
        TryCleanup(coordinator.Dispose, "scroller");
        TryCleanup(listener.Dispose, "window event listener");
    }

    private void TryCleanup(Action cleanup, string component)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to clean up {Component}", component);
        }
    }

    private void HandleScrollTimerTick(object? sender, EventArgs args) =>
        coordinator.OnTick();
}