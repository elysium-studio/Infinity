namespace Infinity.Application.Abstractions;

public interface IWindowStore :
    IReadOnlyCollection<TrackedWindow>
{
    new WindowStoreEnumerator GetEnumerator();

    bool TryGet(IntPtr windowHandle, out TrackedWindow window);

    void Add(TrackedWindow window);

    void Remove(IntPtr windowHandle);

    void NotifyChanged(IntPtr windowHandle);

    event EventHandler<TrackedWindow>? WindowAdded;

    event EventHandler<IntPtr>? WindowRemoved;

    event EventHandler<TrackedWindow>? WindowChanged;
}
