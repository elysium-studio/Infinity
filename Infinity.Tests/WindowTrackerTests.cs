using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class WindowTrackerTests
{
    [Fact]
    public void ExistingWindowKeepsItsPositionDuringStartupRegistration()
    {
        WindowStore store = new();
        WindowTracker tracker = CreateTracker(store, new TestWindowEventListener(), new TestGeometryReader());
        tracker.TryRegisterExisting(new IntPtr(1));
        Assert.True(store.TryGet(new IntPtr(1), out TrackedWindow window));
        Assert.Equal(100, window.CanvasX);
    }


    [Fact]
    public async Task MinimizedWindowReturnsToItsStoredPage()
    {
        WindowStore store = new();
        TestWindowEventListener listener = new();
        TestGeometryReader geometry = new();
        TestPanState state = new();
        state.SetOffset(1000);
        WindowTracker tracker = CreateTracker(store, listener, geometry, state);
        tracker.Start();
        try
        {
            tracker.TryRegisterExisting(new IntPtr(5));
            Assert.True(store.TryGet(new IntPtr(5), out TrackedWindow window));
            Assert.Equal(1100, window.CanvasX);
            state.SetOffset(0);
            TaskCompletionSource removed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            store.WindowRemoved += (_, handle) =>  {  if (handle == new IntPtr(5))  {  removed.TrySetResult();  }  };
            geometry.IsWindowMinimised = true;
            listener.RaiseMinimizeStarted(new IntPtr(5));
            await removed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            geometry.IsWindowMinimised = false;
            listener.RaiseMinimizeEnded(new IntPtr(5));
            Assert.True(store.TryGet(new IntPtr(5), out TrackedWindow restoredWindow));
            Assert.Equal(1100, restoredWindow.CanvasX);
        }
        finally
        {
            tracker.Stop();
        }
    }


    [Fact]
    public void ManagedDragRemainsAtItsViewportPositionDuringPageScroll()
    {
        WindowStore store = new();
        TestWindowEventListener listener = new();
        TestPanState state = new();
        TestTrackedWindowDragController dragController = new()
        {
            DraggingWindow = new(5)
        };
        WindowTracker tracker = CreateTracker(store, listener, new TestGeometryReader(), state, dragController);
        tracker.Start();
        try
        {
            tracker.TryRegisterExisting(new IntPtr(5));
            state.SetOffset(2000);
            Assert.True(store.TryGet(new IntPtr(5), out TrackedWindow window));
            Assert.Equal(2100, window.CanvasX);
        }
        finally
        {
            tracker.Stop();
        }
    }


    [Fact]
    public void ResizeAtSamePositionRefreshesTrackedWindowGeometry()
    {
        WindowStore store = new();
        TestWindowEventListener listener = new();
        TestGeometryReader geometry = new();
        WindowTracker tracker = CreateTracker(store, listener, geometry);
        tracker.Start();
        try
        {
            tracker.TryRegisterExisting(new IntPtr(5));
            TrackedWindow? changedWindow = null;
            store.WindowChanged += (_, window) => changedWindow = window;
            geometry.Width = 1280;
            geometry.Height = 720;
            listener.RaiseWindowLocationChanged(new IntPtr(5));
            Assert.True(store.TryGet(new IntPtr(5), out TrackedWindow window));
            Assert.Equal(100, window.CanvasX);
            Assert.Equal(200, window.CanvasY);
            Assert.Equal(1280, window.Width);
            Assert.Equal(720, window.Height);
            Assert.Same(window, changedWindow);
        }
        finally
        {
            tracker.Stop();
        }
    }


    private static WindowTracker CreateTracker(IWindowStore store, IWindowEventListener listener, IWindowGeometryReader geometry, IPanState? state = null, ITrackedWindowDragController? trackedWindowDragController = null)
    {
        TestWindowFilter filter = new();
        TestWindowEnumerator enumerator = new();
        return new(store, geometry, filter, new TestAncestorResolver(), new TestRestoreGuard(), new TestPageTransitionGuard(), new TestMoveGuard(), new TestDragGuard(), trackedWindowDragController ?? new TestTrackedWindowDragController(), new WindowTrackingReconciler(store, filter, enumerator, new IntPtr(99)), listener, state ?? new TestPanState(), new TestDispatcher(), NullLogger<WindowTracker>.Instance, new IntPtr(99));
    }


    private sealed class TestGeometryReader : IWindowGeometryReader
    {
        public bool IsWindowVisible { get; set; } = true;

        public bool IsWindowMinimised { get; set; }

        public int X { get; set; } = 100;

        public int Y { get; set; } = 200;

        public int Width { get; set; } = 800;

        public int Height { get; set; } = 600;

        public bool IsMinimised(IntPtr windowHandle) => IsWindowMinimised;

        public bool IsVisible(IntPtr windowHandle) => IsWindowVisible;

        public bool TryReadGeometry(IntPtr windowHandle, out int x, out int y, out int width, out int height)
        {
            x = X;
            y = Y;
            width = Width;
            height = Height;
            return true;
        }


        public bool TryReadVisibleGeometry(IntPtr windowHandle, out int x, out int y, out int width, out int height) => TryReadGeometry(windowHandle, out x, out y, out width, out height);
    }


    private sealed class TestWindowFilter : IWindowFilter
    {
        public bool ShouldTrack(IntPtr windowHandle, IntPtr ownerHandle) => true;
    }


    private sealed class TestAncestorResolver : IWindowAncestorResolver
    {
        public IntPtr GetRootAncestor(IntPtr windowHandle) => windowHandle;
    }


    private sealed class TestRestoreGuard : IWindowRestoreGuard
    {
        public bool IsRestoring(IntPtr windowHandle) => false;

        public void MarkRestoring(IntPtr windowHandle)
        {
        }
    }


    private sealed class TestPageTransitionGuard : IWindowPageTransitionGuard
    {
        public void PreservePage(nint windowHandle, int page, int workspaceWidth, int workAreaX)
        {
        }


        public bool TryMapToPreservedPage(nint windowHandle, int candidateCanvasX, int windowWidth, out int mappedCanvasX)
        {
            mappedCanvasX = candidateCanvasX;
            return false;
        }


        public void Clear(nint windowHandle)
        {
        }
    }


    private sealed class TestMoveGuard : IWindowMoveGuard
    {
        public bool IsSystemMove => false;

        public WindowMoveScope Begin() => new(() =>  {  });
    }


    private sealed class TestDragGuard : IWindowDragGuard
    {
        public event Action? HoldStarted;

        public IntPtr DraggingWindow => IntPtr.Zero;

        public bool IsAnyDragging => false;

        public bool IsDragging(IntPtr windowHandle) => false;

        public void Start() => HoldStarted?.Invoke();

        public void Stop()
        {
        }
    }


    private sealed class TestTrackedWindowDragController : ITrackedWindowDragController
    {
        public IntPtr DraggingWindow { get; set; }


        public bool Begin(IntPtr windowHandle) => true;

        public bool MoveTo(IntPtr windowHandle, double canvasX, double canvasY) => true;

        public bool MoveAndResize(IntPtr windowHandle, double canvasX, double canvasY, double width, double height) => true;

        public void End(IntPtr windowHandle)
        {
        }
    }


    private sealed class TestWindowEnumerator : IWindowEnumerator
    {
        public void EnumerateVisible(Action<IntPtr> onWindowFound)
        {
        }
    }


    private sealed class TestWindowEventListener : IWindowEventListener
    {
        public event Action<IntPtr>? WindowCreated;

        public event Action<IntPtr>? WindowShown;

        public event Action<IntPtr>? MinimizeStarted;

        public event Action<IntPtr>? MinimizeEnded;

        event Action<IntPtr>? IWindowEventListener.WindowDestroyed
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.WindowTitleChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        public event Action<IntPtr>? WindowLocationChanged;

        event Action<IntPtr>? IWindowEventListener.DragStarted
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.DragEnded
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<IntPtr>? IWindowEventListener.ForegroundChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action? IWindowEventListener.WindowStackChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        public void Dispose() => GC.SuppressFinalize(this);

        public void RaiseWindowCreated(IntPtr windowHandle) => WindowCreated?.Invoke(windowHandle);

        public void RaiseWindowShown(IntPtr windowHandle) => WindowShown?.Invoke(windowHandle);

        public void RaiseMinimizeStarted(IntPtr windowHandle) => MinimizeStarted?.Invoke(windowHandle);

        public void RaiseMinimizeEnded(IntPtr windowHandle) => MinimizeEnded?.Invoke(windowHandle);

        public void RaiseWindowLocationChanged(IntPtr windowHandle) => WindowLocationChanged?.Invoke(windowHandle);

        public void Start()
        {
        }


        public void Stop()
        {
        }
    }


    private sealed class TestPanState : IPanState
    {
        public event Action? OffsetChanged;

        public double MaxOffset => double.MaxValue;

        public double MinOffset => 0;

        public double Offset { get; private set; }


        public void ApplyDelta(double delta)
        {
            Offset += delta;
            OffsetChanged?.Invoke();
        }


        public void SetMaxOffset(double value)
        {
        }


        public void SetOffset(double value)
        {
            Offset = value;
            OffsetChanged?.Invoke();
        }
    }


    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }
}
