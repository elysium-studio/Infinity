namespace Infinity.Application.Abstractions;

public interface IWindowCollection
{
    event EventHandler<TrackedWindow> WindowAdded;

    event EventHandler<IntPtr> WindowRemoved;

    event EventHandler<TrackedWindow> WindowChanged;

    event EventHandler ScrollStopped;

    event EventHandler ZOrderRefreshed;

    event EventHandler WorkspaceLayoutChanged;

    event EventHandler RefreshRequested;

    IEnumerable<TrackedWindow> AllTrackedWindows { get; }

    bool TryGetTrackedWindow(IntPtr handle, out TrackedWindow? trackedWindow);

    void Start();

    void Stop();

    void Queue(bool clearFilter, bool refreshZOrder);

    void QueueReorder();
}