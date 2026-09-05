using Infinity.Application;
using Infinity.Application.Abstractions;

namespace Infinity.Tests;

public sealed class WindowStoreTests
{
    [Fact]
    public void AddStoresWindowAndRaisesAddedEvent()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(1, "First");
        TrackedWindow? added = null;
        store.WindowAdded += (_, value) => added = value;
        store.Add(window);
        Assert.Single(store);
        Assert.True(store.TryGet(window.Handle, out TrackedWindow stored));
        Assert.Same(window, stored);
        Assert.Same(window, added);
    }


    [Fact]
    public void AddWithExistingHandleReplacesInPlaceAndRaisesChangedEvent()
    {
        WindowStore store = new();
        TrackedWindow original = CreateWindow(1, "Original");
        TrackedWindow replacement = CreateWindow(1, "Replacement");
        int changed = 0;
        store.WindowChanged += (_, window) =>
        {
            Assert.Same(replacement, window);
            changed++;
        };
        store.Add(original);
        store.Add(CreateWindow(2, "Second"));
        store.Add(replacement);
        TrackedWindow[] windows = [.. store];
        Assert.Equal(2, windows.Length);
        Assert.Same(replacement, windows[0]);
        Assert.Equal(new IntPtr(2), windows[1].Handle);
        Assert.Equal(1, changed);
    }


    [Fact]
    public void RemoveDeletesKnownWindowAndIgnoresUnknownHandle()
    {
        WindowStore store = new();
        store.Add(CreateWindow(1, "First"));
        List<IntPtr> removed = [];
        store.WindowRemoved += (_, handle) => removed.Add(handle);
        store.Remove(new IntPtr(99));
        store.Remove(new IntPtr(1));
        Assert.Empty(store);
        Assert.Equal([new IntPtr(1)], removed);
    }


    [Fact]
    public void NotifyChangedOnlyRaisesForTrackedWindow()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(1, "First");
        List<TrackedWindow> changed = [];
        store.WindowChanged += (_, value) => changed.Add(value);
        store.Add(window);
        store.NotifyChanged(new IntPtr(99));
        store.NotifyChanged(window.Handle);
        Assert.Equal([window], changed);
    }


    [Fact]
    public void EnumerationSnapshotIsInvalidatedByMutation()
    {
        WindowStore store = new();
        store.Add(CreateWindow(1, "First"));
        Assert.Single(store.ToArray());
        store.Add(CreateWindow(2, "Second"));
        Assert.Equal(2, store.ToArray().Length);
    }


    [Fact]
    public void InvalidatePlacementMarksBothCoordinatesAsUnknown()
    {
        TrackedWindow window = CreateWindow(1, "First");
        window.LastPlacedX = 100;
        window.LastPlacedY = 200;
        window.InvalidatePlacement();
        Assert.Equal(int.MinValue, window.LastPlacedX);
        Assert.Equal(int.MinValue, window.LastPlacedY);
    }


    private static TrackedWindow CreateWindow(int handle, string title) => new()
    {
        Handle = new(handle),
        CanvasX = 100,
        CanvasY = 200,
        Width = 800,
        Height = 600,
        Title = title
    };
}
