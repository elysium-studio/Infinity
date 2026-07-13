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
    IPanState state,
    IWindowStore repository,
    IWindowConcealmentRecovery concealmentRecovery,
    IWorkspace workspace,
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

        TrackedWindow? fullyOffscreenWindow = repository
            .Where(window => window.CanvasY < workspace.Height &&
                (long)window.CanvasY + window.Height > 0)
            .Where(window => (long)window.CanvasX + window.Width <= 0)
            .MinBy(window => (long)window.CanvasX + window.Width);

        if (fullyOffscreenWindow is not null)
        {
            long rightEdge = (long)fullyOffscreenWindow.CanvasX + fullyOffscreenWindow.Width;
            long bottomEdge = (long)fullyOffscreenWindow.CanvasY + fullyOffscreenWindow.Height;
            int pageShift = checked((int)(((-rightEdge / workspace.Width) + 1) * workspace.Width));
            logger.LogInformation(
                "Fully offscreen window detected during startup. Handle={WindowHandle}, Left={WindowLeft}, Top={WindowTop}, Right={WindowRight}, Bottom={WindowBottom}, PageShift={PageShift}",
                fullyOffscreenWindow.Handle,
                fullyOffscreenWindow.CanvasX,
                fullyOffscreenWindow.CanvasY,
                rightEdge,
                bottomEdge,
                pageShift);

            foreach (TrackedWindow trackedWindow in repository)
            {
                trackedWindow.CanvasX += pageShift;
            }

            state.SetOffset(pageShift);
        }

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
