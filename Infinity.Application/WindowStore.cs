using System.Collections;
using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class WindowStore : IWindowStore
{
    private readonly Lock syncRoot = new();
    private readonly Dictionary<IntPtr, TrackedWindow> lookup = [];
    private readonly List<TrackedWindow> ordered = [];
    private TrackedWindow[]? cachedAll;

    public event EventHandler<TrackedWindow>? WindowAdded;

    public event EventHandler<IntPtr>? WindowRemoved;

    public event EventHandler<TrackedWindow>? WindowChanged;

    public int Count
    {
        get
        {
            lock (syncRoot)
            {
                return ordered.Count;
            }
        }
    }


    public bool TryGet(IntPtr windowHandle, out TrackedWindow window)
    {
        lock (syncRoot)
        {
            return lookup.TryGetValue(windowHandle, out window!);
        }
    }


    public void Add(TrackedWindow window)
    {
        bool added;
        lock (syncRoot)
        {
            added = !lookup.TryGetValue(window.Handle, out TrackedWindow? existing);
            if (added)
            {
                ordered.Add(window);
            }
            else
            {
                int index = ordered.IndexOf(existing!);
                if (index >= 0)
                {
                    ordered[index] = window;
                }
                else
                {
                    ordered.Add(window);
                }
            }

            lookup[window.Handle] = window;
            cachedAll = null;
        }

        if (added)
        {
            WindowAdded?.Invoke(this, window);
        }
        else
        {
            WindowChanged?.Invoke(this, window);
        }
    }


    public void Remove(IntPtr windowHandle)
    {
        TrackedWindow? window;
        lock (syncRoot)
        {
            if (!lookup.Remove(windowHandle, out window))
            {
                return;
            }

            ordered.Remove(window);
            cachedAll = null;
        }

        WindowRemoved?.Invoke(this, windowHandle);
    }


    public void NotifyChanged(IntPtr windowHandle)
    {
        TrackedWindow? window;
        lock (syncRoot)
        {
            if (!lookup.TryGetValue(windowHandle, out window))
            {
                return;
            }
        }

        WindowChanged?.Invoke(this, window);
    }


    public WindowStoreEnumerator GetEnumerator()
    {
        lock (syncRoot)
        {
            return new(cachedAll ??= [..ordered]);
        }
    }


    IEnumerator<TrackedWindow> IEnumerable<TrackedWindow>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
