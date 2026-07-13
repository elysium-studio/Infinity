using Infinity.Application;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public class TrackedWindowDragControllerTests
{
    [Fact]
    public void MoveUsesWorkspaceDeltaAndRepositionsWindow()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(2250, 100);
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = 2000 };
        TrackedWindowDragController controller = new(store,
            scroller,
            NullLogger<TrackedWindowDragController>.Instance);
        int changedCount = 0;
        store.WindowChanged += (_, _) => changedCount++;

        bool began = controller.Begin(window.Handle);
        bool moved = controller.Move(window.Handle, 500, 50);

        Assert.True(began);
        Assert.True(moved);
        Assert.Equal(window.Handle, controller.DraggingWindow);
        Assert.Equal(2750, window.CanvasX);
        Assert.Equal(150, window.CanvasY);
        Assert.Equal(1, changedCount);
        Assert.Equal(1, scroller.ResetCount);
        Assert.Equal(1, scroller.RepositionCount);
    }

    [Fact]
    public void MoveKeepsWindowUnderPointerAfterPageScroll()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(2250, 100);
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = 2000 };
        TrackedWindowDragController controller = new(store,
            scroller,
            NullLogger<TrackedWindowDragController>.Instance);

        Assert.True(controller.Begin(window.Handle));
        scroller.VisualOffset = 3000;

        Assert.True(controller.Move(window.Handle, 100, 0));
        Assert.Equal(3350, window.CanvasX);
    }

    [Fact]
    public void MoveUpdatesStickyWindowViewportAnchor()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(2250, 100);
        window.IsSticky = true;
        window.StickyViewportX = 250;
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = 2000 };
        TrackedWindowDragController controller = new(store,
            scroller,
            NullLogger<TrackedWindowDragController>.Instance);

        Assert.True(controller.Begin(window.Handle));
        Assert.True(controller.Move(window.Handle, -75, 25));

        Assert.Equal(175, window.StickyViewportX);
        Assert.Equal(2175, window.CanvasX);
        Assert.Equal(125, window.CanvasY);
    }

    [Fact]
    public void EndClearsOnlyMatchingDragAndRaisesCompletion()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250, 100);
        TrackedWindow secondWindow = CreateWindow(500, 200) with { Handle = new IntPtr(2) };
        store.Add(window);
        store.Add(secondWindow);
        TrackedWindowDragController controller = new(store,
            new TestScroller(),
            NullLogger<TrackedWindowDragController>.Instance);
        int endedCount = 0;
        controller.DragEnded += () => endedCount++;

        Assert.True(controller.Begin(window.Handle));
        Assert.False(controller.Begin(new IntPtr(2)));

        controller.End(new IntPtr(2));
        Assert.Equal(window.Handle, controller.DraggingWindow);

        controller.End(window.Handle);
        Assert.Equal(IntPtr.Zero, controller.DraggingWindow);
        Assert.Equal(1, endedCount);
    }

    [Fact]
    public void RemovedWindowEndsActiveDrag()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250, 100);
        store.Add(window);
        TrackedWindowDragController controller = new(store,
            new TestScroller(),
            NullLogger<TrackedWindowDragController>.Instance);
        int endedCount = 0;
        controller.DragEnded += () => endedCount++;

        Assert.True(controller.Begin(window.Handle));
        store.Remove(window.Handle);

        Assert.False(controller.Move(window.Handle, 50, 0));
        Assert.Equal(IntPtr.Zero, controller.DraggingWindow);
        Assert.Equal(1, endedCount);
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
            NullLogger<TrackedWindowDragController>.Instance);

        Assert.True(controller.Begin(window.Handle));
        Assert.False(controller.Move(window.Handle, double.NaN, 0));
        Assert.Equal(250, window.CanvasX);
        Assert.Equal(100, window.CanvasY);
        Assert.Equal(0, scroller.RepositionCount);
    }

    private static TrackedWindow CreateWindow(int canvasX, int canvasY) => new()
    {
        Handle = new IntPtr(1),
        CanvasX = canvasX,
        CanvasY = canvasY,
        Width = 800,
        Height = 600
    };
}
