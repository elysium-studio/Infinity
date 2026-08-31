using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class TrackedWindowDragControllerTests
{
    [Fact]
    public void MoveToUsesCanvasPositionAndRepositionsWindow()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(2250, 100);
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = 2000 };
        TrackedWindowDragController controller = new(store,
            scroller,
            new TestWindowResizeSynchronizer(),
            NullLogger<TrackedWindowDragController>.Instance);
        int changedCount = 0;
        store.WindowChanged += (_, _) => changedCount++;

        bool began = controller.Begin(window.Handle);
        bool moved = controller.MoveTo(window.Handle, 2750, 150);

        Assert.True(began);
        Assert.True(moved);
        Assert.Equal(window.Handle, controller.DraggingWindow);
        Assert.Equal(2750, window.CanvasX);
        Assert.Equal(150, window.CanvasY);
        Assert.Equal(0, changedCount);
        Assert.Equal(1, scroller.ResetCount);
        Assert.Equal(1, scroller.RepositionCount);

        controller.End(window.Handle);

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void EndClearsOnlyMatchingDrag()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250, 100);
        TrackedWindow secondWindow = CreateWindow(500, 200) with { Handle = new IntPtr(2) };
        store.Add(window);
        store.Add(secondWindow);
        TrackedWindowDragController controller = new(store,
            new TestScroller(),
            new TestWindowResizeSynchronizer(),
            NullLogger<TrackedWindowDragController>.Instance);
        Assert.True(controller.Begin(window.Handle));
        Assert.False(controller.Begin(new IntPtr(2)));

        controller.End(new IntPtr(2));
        Assert.Equal(window.Handle, controller.DraggingWindow);

        controller.End(window.Handle);
        Assert.Equal(IntPtr.Zero, controller.DraggingWindow);
    }

    [Fact]
    public void MoveToUsesExactCanvasPositionRegardlessOfScrollOffset()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(2250, 100);
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = 6000 };
        TrackedWindowDragController controller = new(store, scroller, new TestWindowResizeSynchronizer(), NullLogger<TrackedWindowDragController>.Instance);

        Assert.True(controller.Begin(window.Handle));
        Assert.True(controller.MoveTo(window.Handle, 250, 175));
        Assert.Equal(250, window.CanvasX);
        Assert.Equal(175, window.CanvasY);
        Assert.Equal(1, scroller.RepositionCount);
    }

    [Fact]
    public void RemovedWindowEndsActiveDrag()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250, 100);
        store.Add(window);
        TrackedWindowDragController controller = new(store,
            new TestScroller(),
            new TestWindowResizeSynchronizer(),
            NullLogger<TrackedWindowDragController>.Instance);
        Assert.True(controller.Begin(window.Handle));
        store.Remove(window.Handle);

        Assert.False(controller.MoveTo(window.Handle, 300, 100));
        Assert.Equal(IntPtr.Zero, controller.DraggingWindow);
    }

    [Fact]
    public void InvalidMovementDoesNotChangeTrackedWindow()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250, 100);
        store.Add(window);
        TestScroller scroller = new();
        TrackedWindowDragController controller = new(store,
            scroller,
            new TestWindowResizeSynchronizer(),
            NullLogger<TrackedWindowDragController>.Instance);

        Assert.True(controller.Begin(window.Handle));
        Assert.False(controller.MoveTo(window.Handle, double.NaN, 0));
        Assert.Equal(250, window.CanvasX);
        Assert.Equal(100, window.CanvasY);
        Assert.Equal(0, scroller.RepositionCount);
    }

    [Fact]
    public void MoveAndResizeCommitsNewTrackedGeometry()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250, 100);
        store.Add(window);
        TestScroller scroller = new();
        TestWindowResizeSynchronizer resizeSynchronizer = new();
        TrackedWindowDragController controller = new(store, scroller, resizeSynchronizer, NullLogger<TrackedWindowDragController>.Instance);

        Assert.True(controller.Begin(window.Handle));
        Assert.True(controller.MoveAndResize(window.Handle, 1920, 0, 960, 1080));
        Assert.Equal((1920, 0, 960, 1080), (window.CanvasX, window.CanvasY, window.Width, window.Height));
        Assert.Equal(int.MinValue, window.LastPlacedX);
        Assert.Equal(int.MinValue, window.LastPlacedY);
        Assert.Equal(1, scroller.RepositionCount);
        Assert.Equal((window.Handle, 960, 1080), resizeSynchronizer.LastRequest);
    }

    [Fact]
    public void MoveAndResizeRepairsTheSourceSurfaceWhenSizeIsUnchanged()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250, 100);
        store.Add(window);
        TestWindowResizeSynchronizer resizeSynchronizer = new();
        TrackedWindowDragController controller = new(store, new TestScroller(), resizeSynchronizer, NullLogger<TrackedWindowDragController>.Instance);

        Assert.True(controller.Begin(window.Handle));
        Assert.True(controller.MoveAndResize(window.Handle, 400, 200, window.Width, window.Height));
        Assert.Equal((window.Handle, window.Width, window.Height), resizeSynchronizer.LastRequest);
    }

    private static TrackedWindow CreateWindow(int canvasX, int canvasY) => new()
    {
        Handle = new IntPtr(1),
        CanvasX = canvasX,
        CanvasY = canvasY,
        Width = 800,
        Height = 600
    };

    private sealed class TestWindowResizeSynchronizer :
        IWindowResizeSynchronizer
    {
        public (nint Handle, int Width, int Height)? LastRequest { get; private set; }

        public bool TrySynchronize(nint windowHandle, int width, int height)
        {
            LastRequest = (windowHandle, width, height);
            return true;
        }
    }
}
