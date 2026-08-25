namespace Infinity.Application.Abstractions;

public interface IWindowCollection
{
    event EventHandler<TrackedWindow> WindowAdded;

    event EventHandler<IntPtr> WindowRemoved;

    event EventHandler<TrackedWindow> WindowChanged;

    event EventHandler ScrollStopped;

    event EventHandler WindowStackRefreshed;

    event EventHandler WorkspaceLayoutChanged;

    event EventHandler RefreshRequested;

    IEnumerable<TrackedWindow> AllTrackedWindows { get; }

    bool TryGetTrackedWindow(IntPtr handle, out TrackedWindow? trackedWindow);

    void Queue(bool refreshWindowStack);

    void QueueReorder();

    void Start();

    void Stop();
}
