using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public class WindowDragScrollerTests
{
    [Fact]
    public void ManagedDragParticipatesInModifierDragSession()
    {
        TestPointerInputSource pointer = new();
        TestModifierKeyState modifier = new() { IsActive = true };
        TestTrackedWindowDragController dragController = new() { DraggingWindow = new IntPtr(1) };
        WindowDragScroller dragScroller = new(pointer,
            modifier,
            new TestWindowDragGuard(),
            dragController,
            new TestWorkspace(),
            new TestScroller(),
            new PanState(),
            new TestDispatcher(),
            () => new WindowDragScrollerConfiguration { SpeedLevel = DragScrollSpeed.Normal },
            NullLogger<WindowDragScroller>.Instance);
        int startedCount = 0;
        int movedCount = 0;
        int stoppedCount = 0;
        dragScroller.DragStarted += () => startedCount++;
        dragScroller.DragMoved += () => movedCount++;
        dragScroller.DragStopped += () => stoppedCount++;
        dragScroller.Start();

        try
        {
            dragScroller.UpdateTrackedWindowDragPosition(0.5);

            Assert.Equal(1, startedCount);
            Assert.Equal(0, movedCount);

            dragController.RaiseDragEnded();

            Assert.Equal(1, stoppedCount);
        }
        finally
        {
            dragScroller.Stop();
        }
    }

    [Fact]
    public async Task ManagedDragStartsAndStopsDesktopScrollingAsync()
    {
        TestPointerInputSource pointer = new();
        TestModifierKeyState modifier = new() { IsActive = true };
        TestTrackedWindowDragController dragController = new() { DraggingWindow = new IntPtr(1) };
        TestScroller scroller = new();
        WindowDragScroller dragScroller = new(pointer,
            modifier,
            new TestWindowDragGuard(),
            dragController,
            new TestWorkspace(),
            scroller,
            new PanState(),
            new TestDispatcher(),
            () => new WindowDragScrollerConfiguration { SpeedLevel = DragScrollSpeed.Normal },
            NullLogger<WindowDragScroller>.Instance);
        dragScroller.Start();

        try
        {
            dragScroller.UpdateTrackedWindowDragPosition(1.0);

            Assert.True(dragScroller.IsAutoScrolling);
            double targetOffset = await scroller.ScrollToRequested.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(targetOffset > 0);

            dragController.RaiseDragEnded();

            Assert.False(dragScroller.IsAutoScrolling);
        }
        finally
        {
            dragScroller.Stop();
        }
    }

    [Fact]
    public void ManagedDragCanReverseAutoScrollDirection()
    {
        TestModifierKeyState modifier = new() { IsActive = true };
        TestTrackedWindowDragController dragController = new() { DraggingWindow = new IntPtr(1) };
        TestScroller scroller = new();
        PanState state = new();
        state.SetMaxOffset(10_000);
        state.SetOffset(5_000);
        WindowDragScroller dragScroller = new(new TestPointerInputSource(),
            modifier,
            new TestWindowDragGuard(),
            dragController,
            new TestWorkspace(),
            scroller,
            state,
            new TestDispatcher(),
            () => new WindowDragScrollerConfiguration { SpeedLevel = DragScrollSpeed.Normal },
            NullLogger<WindowDragScroller>.Instance);
        dragScroller.Start();

        try
        {
            dragScroller.UpdateTrackedWindowDragPosition(1.0);

            Assert.True(dragScroller.IsAutoScrolling);
            Assert.True(scroller.VisualOffset > state.Offset);

            dragScroller.UpdateTrackedWindowDragPosition(0.5);

            Assert.False(dragScroller.IsAutoScrolling);

            dragScroller.UpdateTrackedWindowDragPosition(0.0);

            Assert.True(dragScroller.IsAutoScrolling);
            Assert.True(scroller.VisualOffset < state.Offset);
        }
        finally
        {
            dragScroller.Stop();
        }
    }

    [Fact]
    public void ManagedDragDoesNotScrollAwayFromPreviewEdge()
    {
        WindowDragScroller dragScroller = new(new TestPointerInputSource(),
            new TestModifierKeyState { IsActive = true },
            new TestWindowDragGuard(),
            new TestTrackedWindowDragController { DraggingWindow = new IntPtr(1) },
            new TestWorkspace(),
            new TestScroller(),
            new PanState(),
            new TestDispatcher(),
            () => new WindowDragScrollerConfiguration { SpeedLevel = DragScrollSpeed.Normal },
            NullLogger<WindowDragScroller>.Instance);
        dragScroller.Start();

        try
        {
            dragScroller.UpdateTrackedWindowDragPosition(0.5);

            Assert.False(dragScroller.IsAutoScrolling);
        }
        finally
        {
            dragScroller.Stop();
        }
    }

    [Fact]
    public void ManagedDragUsesNativeEdgeThreshold()
    {
        WindowDragScroller dragScroller = new(new TestPointerInputSource(),
            new TestModifierKeyState { IsActive = true },
            new TestWindowDragGuard(),
            new TestTrackedWindowDragController { DraggingWindow = new IntPtr(1) },
            new TestWorkspace(),
            new TestScroller(),
            new PanState(),
            new TestDispatcher(),
            () => new WindowDragScrollerConfiguration { SpeedLevel = DragScrollSpeed.Normal },
            NullLogger<WindowDragScroller>.Instance);
        dragScroller.Start();

        try
        {
            dragScroller.UpdateTrackedWindowDragPosition(0.9);

            Assert.True(dragScroller.IsAutoScrolling);
        }
        finally
        {
            dragScroller.Stop();
        }
    }

    [Fact]
    public void NativeDragStillPublishesMovement()
    {
        TestPointerInputSource pointer = new();
        TestWindowDragGuard dragGuard = new() { IsAnyDragging = true };
        WindowDragScroller dragScroller = new(pointer,
            new TestModifierKeyState { IsActive = true },
            dragGuard,
            new TestTrackedWindowDragController(),
            new TestWorkspace(),
            new TestScroller(),
            new PanState(),
            new TestDispatcher(),
            () => new WindowDragScrollerConfiguration { SpeedLevel = DragScrollSpeed.Normal },
            NullLogger<WindowDragScroller>.Instance);
        int movedCount = 0;
        dragScroller.DragMoved += () => movedCount++;
        dragScroller.Start();

        try
        {
            pointer.RaiseCursorMoved(960, 500);

            Assert.Equal(1, movedCount);
        }
        finally
        {
            dragScroller.Stop();
        }
    }

    private class TestPointerInputSource : IPointerInputSource
    {
        public event Action<int, int>? CursorMoved;

        public event Action? LeftButtonClicked;

        public event Action? MiddleButtonClicked;

        public event Action? RightButtonClicked;

        public event Action<int>? ScrollDeltaReceived;

        public event Action<double>? ScrollVelocityIdle;

        public bool TryGetCursorPosition(out int x, out int y)
        {
            x = 0;
            y = 0;
            return false;
        }

        public void RaiseCursorMoved(int x, int y) => CursorMoved?.Invoke(x, y);

        public void Dispose()
        {
            LeftButtonClicked?.Invoke();
            MiddleButtonClicked?.Invoke();
            RightButtonClicked?.Invoke();
            ScrollDeltaReceived?.Invoke(0);
            ScrollVelocityIdle?.Invoke(0);
            GC.SuppressFinalize(this);
        }
    }

    private class TestModifierKeyState : IModifierKeyState
    {
        public event Action<bool>? StateChanged;

        public bool IsActive { get; set; }

        public void SetKeys(List<List<int>> combinations)
        {
        }

        public void RaiseStateChanged(bool active)
        {
            IsActive = active;
            StateChanged?.Invoke(active);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    private class TestWindowDragGuard : IWindowDragGuard
    {
        public event Action? HoldStarted;

        public bool IsAnyDragging { get; set; }

        public IntPtr DraggingWindow => IsAnyDragging ? new IntPtr(1) : IntPtr.Zero;

        public bool IsDragging(IntPtr windowHandle) => false;

        public void Start() => HoldStarted?.Invoke();

        public void Stop()
        {
        }
    }

    private class TestTrackedWindowDragController : ITrackedWindowDragController
    {
        public event Action? DragEnded;

        public IntPtr DraggingWindow { get; set; }

        public bool Begin(IntPtr windowHandle) => true;

        public bool Move(IntPtr windowHandle, double horizontalDelta, double verticalDelta) => true;

        public void End(IntPtr windowHandle) => RaiseDragEnded();

        public void RaiseDragEnded()
        {
            DraggingWindow = IntPtr.Zero;
            DragEnded?.Invoke();
        }
    }

    private class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => 1080;

        public int Width => 1920;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }

    private class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }
}
