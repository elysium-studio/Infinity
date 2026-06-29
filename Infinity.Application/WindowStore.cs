using Infinity.Application.Abstractions;
using System.Collections;

namespace Infinity.Application;

public class WindowStore :
    IWindowStore
{
    private readonly Dictionary<IntPtr, TrackedWindow> lookup = new();
    private readonly List<TrackedWindow> ordered = new();
    private TrackedWindow[]? cachedAll;

    public event EventHandler<TrackedWindow>? WindowAdded;

    public event EventHandler<IntPtr>? WindowRemoved;

    public event EventHandler<TrackedWindow>? WindowChanged;

    public int Count => ordered.Count;

    public bool TryGet(IntPtr windowHandle, out TrackedWindow window) =>
        lookup.TryGetValue(windowHandle, out window!);

    public void Add(TrackedWindow window)
    {
        if (lookup.TryGetValue(window.Handle, out TrackedWindow? existing))
        {
            int index = ordered.IndexOf(existing);

            if (index >= 0)
            {
                ordered[index] = window;
            }
            else
            {
                ordered.Add(window);
            }
        }
        else
        {
            ordered.Add(window);
        }

        lookup[window.Handle] = window;
        cachedAll = null;

        WindowAdded?.Invoke(this, window);
    }

    public void Remove(IntPtr windowHandle)
    {
        if (lookup.Remove(windowHandle, out TrackedWindow? window))
        {
            ordered.Remove(window);
            cachedAll = null;

            WindowRemoved?.Invoke(this, windowHandle);
        }
    }

    public void NotifyChanged(IntPtr windowHandle)
    {
        if (lookup.TryGetValue(windowHandle, out TrackedWindow? window))
        {
            WindowChanged?.Invoke(this, window);
        }
    }

    public WindowStoreEnumerator GetEnumerator() => new(cachedAll ??= ordered.ToArray());

    IEnumerator<TrackedWindow> IEnumerable<TrackedWindow>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}