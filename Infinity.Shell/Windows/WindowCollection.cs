using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell;

public class WindowCollection(IWindowStore store,
    IScrollTimer timer,
    IScroller scroller,
    IWindowZOrder zOrder,
    IWorkspace workspace,
    IWindowFilterState filterState,
    IWindowPageCoordinator coordinator,
    ITrackedWindowCollection trackedWindowCollection,
    IDispatcher dispatcher,
    ILogger<WindowCollection> logger) :
    IWindowCollection
{
    private readonly Lock refreshSyncRoot = new();
    private readonly Lock reorderSyncRoot = new();

    private bool refreshQueued;
    private bool reorderQueued;
    private bool queuedRefreshShouldClearFilter;
    private bool queuedRefreshShouldRefreshZOrder;

    public event EventHandler<TrackedWindow>? WindowAdded;

    public event EventHandler<IntPtr>? WindowRemoved;

    public event EventHandler<TrackedWindow>? WindowChanged;

    public event EventHandler? ScrollStopped;

    public event EventHandler? ZOrderRefreshed;

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
        zOrder.ZOrderChanged += HandleZOrderChanged;
        zOrder.FocusedWindowChanged += HandleFocusedWindowChanged;
        workspace.WorkspaceLayoutChanged += HandleWorkspaceLayoutChanged;

        HashSet<IntPtr> activeHandles = [.. store.Select(trackedWindow => trackedWindow.Handle)];

        foreach (IntPtr staleHandle in trackedWindowCollection.All.Select(trackedWindow => trackedWindow.Handle)
            .Where(handle => !activeHandles.Contains(handle)).ToList())
        {
            WindowRemoved?.Invoke(this, staleHandle);
        }

        zOrder.Refresh();
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
        zOrder.ZOrderChanged -= HandleZOrderChanged;
        zOrder.FocusedWindowChanged -= HandleFocusedWindowChanged;
        workspace.WorkspaceLayoutChanged -= HandleWorkspaceLayoutChanged;

        lock (refreshSyncRoot)
        {
            refreshQueued = false;
            queuedRefreshShouldClearFilter = false;
            queuedRefreshShouldRefreshZOrder = false;
        }

        lock (reorderSyncRoot)
        {
            reorderQueued = false;
        }
    }

    public void Queue(bool clearFilter, bool refreshZOrder)
    {
        bool shouldQueue;

        lock (refreshSyncRoot)
        {
            queuedRefreshShouldClearFilter |= clearFilter;
            queuedRefreshShouldRefreshZOrder |= refreshZOrder;

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

    private void HandleWindowChanged(object? sender, TrackedWindow trackedWindow)
    {
        dispatcher.Dispatch(() =>
        {
            WindowChanged?.Invoke(this, trackedWindow);
        });
    }

    private void HandleScrollTick(object? sender, EventArgs args) =>
        Queue(false, false);

    private void HandleScrollStopped(object? sender, EventArgs args) =>
        ScrollStopped?.Invoke(this, EventArgs.Empty);

    private void HandleZOrderChanged(object? sender, EventArgs args) =>
        QueueReorder();

    private void HandleFocusedWindowChanged(object? sender, IntPtr handle) =>
        coordinator.HandleFocusChanged(handle);

    private void HandleWorkspaceLayoutChanged(object? sender, EventArgs args)
    {
        logger.LogInformation("Workspace layout changed");

        Queue(false, false);
        WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessQueuedRefresh()
    {
        bool shouldClearFilter;
        bool shouldRefreshZOrder;

        lock (refreshSyncRoot)
        {
            refreshQueued = false;
            shouldClearFilter = queuedRefreshShouldClearFilter;
            shouldRefreshZOrder = queuedRefreshShouldRefreshZOrder;
            queuedRefreshShouldClearFilter = false;
            queuedRefreshShouldRefreshZOrder = false;
        }

        if (shouldClearFilter && filterState.IsActive)
        {
            filterState.Filter = string.Empty;
        }

        if (shouldRefreshZOrder)
        {
            zOrder.Refresh();
        }

        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessQueuedReorder()
    {
        lock (reorderSyncRoot)
        {
            reorderQueued = false;
        }

        foreach (TrackedWindow trackedWindow in store)
        {
            if (trackedWindowCollection.TryGet(trackedWindow.Handle, out ITrackedWindow? windowViewModel))
            {
                windowViewModel!.ZIndex = trackedWindow.ZIndex;
            }
        }

        ZOrderRefreshed?.Invoke(this, EventArgs.Empty);
    }
}