using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Tests;

public class WindowTrackerPlacementRuleTests
{
    [Fact]
    public void ExistingWindowKeepsItsPositionDuringStartupRegistration()
    {
        WindowStore store = new();
        TestWindowMover mover = new();
        WindowTracker tracker = CreateTracker(store, mover, new TestWindowEventListener(), new TestGeometryReader());

        tracker.TryRegisterExisting(new IntPtr(1));

        Assert.True(store.TryGet(new IntPtr(1), out TrackedWindow window));
        Assert.Equal(100, window.CanvasX);
        Assert.Equal(0, mover.MoveCount);
    }

    [Fact]
    public void NewlyCreatedWindowReceivesPlacementRule()
    {
        WindowStore store = new();
        TestWindowMover mover = new();
        TestWindowEventListener listener = new();
        WindowTracker tracker = CreateTracker(store, mover, listener, new TestGeometryReader());
        tracker.Start();

        try
        {
            listener.RaiseWindowCreated(new IntPtr(2));

            Assert.True(store.TryGet(new IntPtr(2), out TrackedWindow window));
            Assert.Equal(2100, window.CanvasX);
            Assert.Equal(1, mover.MoveCount);
            Assert.Equal(2100, mover.LastX);
        }
        finally
        {
            tracker.Stop();
        }
    }

    [Fact]
    public void HiddenNewWindowRetainsCreationPolicyUntilShown()
    {
        WindowStore store = new();
        TestWindowMover mover = new();
        TestWindowEventListener listener = new();
        TestGeometryReader geometry = new() { IsWindowVisible = false };
        WindowTracker tracker = CreateTracker(store, mover, listener, geometry);
        tracker.Start();

        try
        {
            listener.RaiseWindowCreated(new IntPtr(3));
            Assert.False(store.TryGet(new IntPtr(3), out _));

            geometry.IsWindowVisible = true;
            listener.RaiseWindowShown(new IntPtr(3));

            Assert.True(store.TryGet(new IntPtr(3), out TrackedWindow window));
            Assert.Equal(2100, window.CanvasX);
            Assert.Equal(1, mover.MoveCount);
        }
        finally
        {
            tracker.Stop();
        }
    }

    [Fact]
    public async Task MinimizedStickyWindowReturnsAtItsViewportAnchor()
    {
        WindowStore store = new();
        TestWindowMover mover = new();
        TestWindowEventListener listener = new();
        TestGeometryReader geometry = new();
        TestPanState state = new();
        WindowTracker tracker = CreateTracker(store, mover, listener, geometry, state);
        tracker.Start();

        try
        {
            tracker.TryRegisterExisting(new IntPtr(4));
            Assert.True(store.TryGet(new IntPtr(4), out TrackedWindow window));
            window.IsSticky = true;
            window.StickyViewportX = 100;
            TaskCompletionSource removed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            store.WindowRemoved += (_, handle) =>
            {
                if (handle == new IntPtr(4))
                {
                    removed.TrySetResult();
                }
            };

            geometry.IsWindowMinimised = true;
            listener.RaiseMinimizeStarted(new IntPtr(4));
            await removed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            state.SetOffset(2000);
            geometry.IsWindowMinimised = false;
            listener.RaiseMinimizeEnded(new IntPtr(4));

            Assert.True(store.TryGet(new IntPtr(4), out TrackedWindow restoredWindow));
            Assert.True(restoredWindow.IsSticky);
            Assert.Equal(100, restoredWindow.StickyViewportX);
            Assert.Equal(2100, restoredWindow.CanvasX);
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
        TestTrackedWindowDragController dragController = new() { DraggingWindow = new IntPtr(5) };
        WindowTracker tracker = CreateTracker(store,
            new TestWindowMover(),
            listener,
            new TestGeometryReader(),
            state,
            dragController);
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

    private static WindowTracker CreateTracker(IWindowStore store,
        IWindowMover mover,
        IWindowEventListener listener,
        IWindowGeometryReader geometry,
        IPanState? state = null,
        ITrackedWindowDragController? trackedWindowDragController = null) =>
        new(store,
            geometry,
            new TestWindowFilter(),
            new TestAncestorResolver(),
            new TestRestoreGuard(),
            new TestPlacementRules(),
            new TestMoveGuard(),
            mover,
            new TestConcealer(),
            new TestDragGuard(),
            trackedWindowDragController ?? new TestTrackedWindowDragController(),
            new TestWindowEnumerator(),
            listener,
            new TestWorkspace(),
            new TestPager(),
            state ?? new TestPanState(),
            new TestDispatcher(),
            new IntPtr(99));

    private class TestGeometryReader : IWindowGeometryReader
    {
        public bool IsWindowVisible { get; set; } = true;

        public bool IsWindowMinimised { get; set; }

        public bool IsMinimised(IntPtr windowHandle) => IsWindowMinimised;

        public bool IsVisible(IntPtr windowHandle) => IsWindowVisible;

        public bool TryReadGeometry(IntPtr windowHandle, out int x, out int y, out int width, out int height)
        {
            x = 100;
            y = 200;
            width = 800;
            height = 600;
            return true;
        }
    }

    private class TestWindowFilter : IWindowFilter
    {
        public bool ShouldTrack(IntPtr windowHandle, IntPtr ownerHandle) => true;
    }

    private class TestAncestorResolver : IWindowAncestorResolver
    {
        public IntPtr GetRootAncestor(IntPtr windowHandle) => windowHandle;
    }

    private class TestRestoreGuard : IWindowRestoreGuard
    {
        public bool IsRestoring(IntPtr windowHandle) => false;

        public void MarkRestoring(IntPtr windowHandle)
        {
        }
    }

    private class TestPlacementRules : IWindowPlacementRules
    {
        public bool CanCreateRule(IntPtr windowHandle) => true;

        public Task<bool> RemoveAsync(IntPtr windowHandle) => Task.FromResult(true);

        public Task<bool> SetTargetPageAsync(IntPtr windowHandle, int targetPage) => Task.FromResult(true);

        public bool TryGetTargetPage(IntPtr windowHandle, out int targetPage)
        {
            targetPage = 2;
            return true;
        }
    }

    private class TestMoveGuard : IWindowMoveGuard
    {
        public bool IsSystemMove => false;

        public WindowMoveScope Begin() => new(() => { });
    }

    private class TestWindowMover : IWindowMover
    {
        public int LastX { get; private set; }

        public int MoveCount { get; private set; }

        public void BeginBatch(int count)
        {
        }

        public void EndBatch()
        {
        }

        public void MoveTo(IntPtr windowHandle, int x, int y, int width, int height)
        {
            LastX = x;
            MoveCount++;
        }
    }

    private class TestConcealer : IWindowConcealer
    {
        public bool Conceal(IntPtr windowHandle) => true;

        public IReadOnlySet<IntPtr> ConcealedHandles() => new HashSet<IntPtr>();

        public bool IsConcealed(IntPtr windowHandle) => false;

        public void Reveal(IntPtr windowHandle)
        {
        }
    }

    private class TestDragGuard : IWindowDragGuard
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

    private class TestTrackedWindowDragController : ITrackedWindowDragController
    {
        public event Action? DragEnded;

        public IntPtr DraggingWindow { get; set; }

        public bool Begin(IntPtr windowHandle) => true;

        public bool Move(IntPtr windowHandle, double horizontalDelta, double verticalDelta) => true;

        public void End(IntPtr windowHandle) => DragEnded?.Invoke();
    }

    private class TestWindowEnumerator : IWindowEnumerator
    {
        public void EnumerateVisible(Action<IntPtr> onWindowFound)
        {
        }
    }

    private class TestWindowEventListener : IWindowEventListener
    {
        public event Action<IntPtr>? WindowCreated;

        public event Action<IntPtr>? WindowShown;

        public event Action<IntPtr>? MinimizeStarted;

        public event Action<IntPtr>? MinimizeEnded;

        event Action<IntPtr>? IWindowEventListener.WindowDestroyed
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.WindowTitleChanged
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.WindowLocationChanged
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.DragStarted
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.DragEnded
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.ForegroundChanged
        {
            add { }
            remove { }
        }

        event Action? IWindowEventListener.WindowStackChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void RaiseWindowCreated(IntPtr windowHandle) => WindowCreated?.Invoke(windowHandle);

        public void RaiseWindowShown(IntPtr windowHandle) => WindowShown?.Invoke(windowHandle);

        public void RaiseMinimizeStarted(IntPtr windowHandle) => MinimizeStarted?.Invoke(windowHandle);

        public void RaiseMinimizeEnded(IntPtr windowHandle) => MinimizeEnded?.Invoke(windowHandle);

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private class TestWorkspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Height => 1080;

        public int Width => 1000;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public IntPtr GetCurrentWorkspace()
        {
            WorkspaceLayoutChanged?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
    }

    private class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage => 0;

        public int PageCount => 1;

        public int? MaxPages => null;

        public void NavigateToPage(int page) => PageChanged?.Invoke(page);

        public void SetMaxPages(int? maxPages)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private class TestPanState : IPanState
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

    private class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }
}
