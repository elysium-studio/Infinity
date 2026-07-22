using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public sealed class WindowCollection(IWindowStore store,
    IScrollTimer timer,
    IScroller scroller,
    IWindowStack windowStack,
    IForegroundWindowTracker foregroundWindowTracker,
    IWindowEventListener listener,
    IWorkspace workspace,
    IWindowFilterState filterState,
    IForegroundWindowCoordinator coordinator,
    IDispatcher dispatcher,
    ILogger<WindowCollection> logger) :
    IWindowCollection
{
    private readonly Lock refreshSyncRoot = new();
    private readonly Lock reorderSyncRoot = new();

    private bool refreshQueued;
    private bool reorderQueued;
    private bool queuedRefreshShouldClearFilter;
    private bool queuedRefreshShouldRefreshWindowStack;

    public event EventHandler<TrackedWindow>? WindowAdded;

    public event EventHandler<IntPtr>? WindowRemoved;

    public event EventHandler<TrackedWindow>? WindowChanged;

    public event EventHandler? ScrollStopped;

    public event EventHandler? WindowStackRefreshed;

    public event EventHandler? WorkspaceLayoutChanged;

    public event EventHandler? RefreshRequested;

    public IEnumerable<TrackedWindow> AllTrackedWindows => store;

    public bool TryGetTrackedWindow(IntPtr handle, out TrackedWindow? trackedWindow) =>
        store.TryGet(handle, out trackedWindow);

    public void Start()
    {
        logger.LogInformation("Window collection starting");

        store.WindowAdded += HandleWindowAdded;
        store.WindowRemoved += HandleWindowRemoved;
        store.WindowChanged += HandleWindowChanged;
        timer.Tick += HandleScrollTick;
        scroller.ScrollStopped += HandleScrollStopped;
        windowStack.WindowStackChanged += HandleWindowStackChanged;
        foregroundWindowTracker.ForegroundWindowChanged += HandleForegroundWindowChanged;
        listener.MinimizeStarted += HandleWindowMinimizeStarted;
        listener.MinimizeEnded += HandleWindowMinimizeEnded;
        workspace.WorkspaceLayoutChanged += HandleWorkspaceLayoutChanged;

        windowStack.Refresh();
        Queue(false, false);
    }

    public void Stop()
    {
        logger.LogInformation("Window collection stopping");

        store.WindowAdded -= HandleWindowAdded;
        store.WindowRemoved -= HandleWindowRemoved;
        store.WindowChanged -= HandleWindowChanged;
        timer.Tick -= HandleScrollTick;
        scroller.ScrollStopped -= HandleScrollStopped;
        windowStack.WindowStackChanged -= HandleWindowStackChanged;
        foregroundWindowTracker.ForegroundWindowChanged -= HandleForegroundWindowChanged;
        listener.MinimizeStarted -= HandleWindowMinimizeStarted;
        listener.MinimizeEnded -= HandleWindowMinimizeEnded;
        workspace.WorkspaceLayoutChanged -= HandleWorkspaceLayoutChanged;

        lock (refreshSyncRoot)
        {
            refreshQueued = false;
            queuedRefreshShouldClearFilter = false;
            queuedRefreshShouldRefreshWindowStack = false;
        }

        lock (reorderSyncRoot)
        {
            reorderQueued = false;
        }
    }

    public void Queue(bool clearFilter, bool refreshWindowStack)
    {
        bool shouldQueue;

        lock (refreshSyncRoot)
        {
            queuedRefreshShouldClearFilter |= clearFilter;
            queuedRefreshShouldRefreshWindowStack |= refreshWindowStack;

            shouldQueue = !refreshQueued;
            refreshQueued = true;
        }

        if (!shouldQueue)
        {
            return;
        }

        dispatcher.Dispatch(ProcessQueuedRefresh);
    }

    public void QueueReorder()
    {
        bool shouldQueue;

        lock (reorderSyncRoot)
        {
            shouldQueue = !reorderQueued;
            reorderQueued = true;
        }

        if (!shouldQueue)
        {
            return;
        }

        dispatcher.Dispatch(ProcessQueuedReorder);
    }

    private void HandleWindowAdded(object? sender, TrackedWindow trackedWindow)
    {
        logger.LogInformation("Window added: {Title} ({Handle})", trackedWindow.Title, trackedWindow.Handle);

        WindowAdded?.Invoke(this, trackedWindow);
        Queue(true, true);
    }

    private void HandleWindowRemoved(object? sender, IntPtr handle)
    {
        logger.LogInformation("Window removed: {Handle}", handle);

        coordinator.NotifyWindowClosed(handle);

        dispatcher.Dispatch(() =>
        {
            WindowRemoved?.Invoke(this, handle);
            Queue(false, true);
        });
    }

    private void HandleWindowChanged(object? sender, TrackedWindow trackedWindow) =>
        dispatcher.Dispatch(() => WindowChanged?.Invoke(this, trackedWindow));

    private void HandleScrollTick(object? sender, EventArgs args) =>
        Queue(false, false);

    private void HandleScrollStopped(object? sender, EventArgs args) =>
        ScrollStopped?.Invoke(this, EventArgs.Empty);

    private void HandleWindowStackChanged(object? sender, EventArgs args) =>
        QueueReorder();

    private void HandleForegroundWindowChanged(object? sender, IntPtr handle) =>
        dispatcher.Dispatch(() => coordinator.HandleForegroundWindowChanged(handle));

    private void HandleWindowMinimizeStarted(IntPtr handle) =>
        dispatcher.Dispatch(() => coordinator.HandleWindowMinimizeStarted(handle));

    private void HandleWindowMinimizeEnded(IntPtr handle) =>
        dispatcher.Dispatch(() => coordinator.HandleWindowMinimizeEnded(handle));

    private void HandleWorkspaceLayoutChanged(object? sender, EventArgs args)
    {
        logger.LogInformation("Workspace layout changed");

        Queue(false, false);
        WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessQueuedRefresh()
    {
        bool shouldClearFilter;
        bool shouldRefreshWindowStack;

        lock (refreshSyncRoot)
        {
            refreshQueued = false;
            shouldClearFilter = queuedRefreshShouldClearFilter;
            shouldRefreshWindowStack = queuedRefreshShouldRefreshWindowStack;
            queuedRefreshShouldClearFilter = false;
            queuedRefreshShouldRefreshWindowStack = false;
        }

        if (shouldClearFilter && filterState.IsActive)
        {
            filterState.Filter = string.Empty;
        }

        if (shouldRefreshWindowStack)
        {
            windowStack.Refresh();
        }

        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessQueuedReorder()
    {
        lock (reorderSyncRoot)
        {
            reorderQueued = false;
        }

        WindowStackRefreshed?.Invoke(this, EventArgs.Empty);
    }
}
