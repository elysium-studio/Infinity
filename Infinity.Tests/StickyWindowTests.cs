using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class StickyWindowControllerTests
{
    [Fact]
    public void PinCapturesViewportPositionAndRequestsReposition()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(2250);
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = 2000 };
        StickyWindowController controller = new(store, scroller, NullLogger<StickyWindowController>.Instance);
        int changedCount = 0;
        store.WindowChanged += (_, _) => changedCount++;

        bool pinned = controller.Pin(window.Handle);

        Assert.True(pinned);
        Assert.True(window.IsSticky);
        Assert.Equal(250, window.StickyViewportX);
        Assert.Equal(1, changedCount);
        Assert.Equal(1, scroller.RepositionCount);
    }

    [Fact]
    public void UnpinAttachesWindowToCurrentPageAtItsVisiblePosition()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(2250);
        window.IsSticky = true;
        window.StickyViewportX = 250;
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = 4000 };
        StickyWindowController controller = new(store, scroller, NullLogger<StickyWindowController>.Instance);

        bool unpinned = controller.Unpin(window.Handle);

        Assert.True(unpinned);
        Assert.False(window.IsSticky);
        Assert.Equal(4250, window.CanvasX);
        Assert.Equal(0, window.StickyViewportX);
        Assert.Equal(1, scroller.RepositionCount);
    }

    [Fact]
    public void PinRejectsUnknownWindowsAndInvalidOffsets()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(250);
        store.Add(window);
        TestScroller scroller = new() { VisualOffset = double.NaN };
        StickyWindowController controller = new(store, scroller, NullLogger<StickyWindowController>.Instance);

        Assert.False(controller.Pin(new IntPtr(99)));
        Assert.False(controller.Pin(window.Handle));
        Assert.False(window.IsSticky);
        Assert.Equal(0, scroller.RepositionCount);
    }

    private static TrackedWindow CreateWindow(int canvasX) => new()
    {
        Handle = new IntPtr(1),
        CanvasX = canvasX,
        CanvasY = 100,
        Width = 800,
        Height = 600
    };
}

public sealed class StickyWindowScrollingTests
{
    [Fact]
    public void RepositionKeepsStickyWindowAtItsViewportAnchor()
    {
        WindowStore store = new();
        TrackedWindow stickyWindow = CreateWindow(250, true);
        TrackedWindow pageWindow = CreateWindow(2500, false, 2);
        store.Add(stickyWindow);
        store.Add(pageWindow);
        PanState state = new();
        state.SetOffset(2000);
        TestWindowMover mover = new();
        using Scroller scroller = CreateScroller(state, store, mover);

        scroller.Reposition();

        Assert.Equal(2250, stickyWindow.CanvasX);
        Assert.Collection(mover.Moves,
            move => Assert.Equal((stickyWindow.Handle, 250), (move.Handle, move.X)),
            move => Assert.Equal((pageWindow.Handle, 500), (move.Handle, move.X)));
    }

    [Fact]
    public void PresentationSessionDefersWindowMovementUntilExplicitCommit()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(500, false);
        store.Add(window);
        PanState state = new();
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        presentationSession.Begin();
        QueuedDeltaScrollMotion motion = new(100, 100);
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, motion);

        scroller.OnTick();

        Assert.Empty(mover.Moves);
        Assert.Equal(100, state.Offset);

        scroller.OnTick();

        Assert.Empty(mover.Moves);
        Assert.Equal(200, state.Offset);
        Assert.True(presentationSession.IsActive);

        scroller.CommitPresentation();

        Assert.Collection(mover.Moves,
            move => Assert.Equal((window.Handle, 300), (move.Handle, move.X)));
    }

    [Fact]
    public void FirstWheelInputOpensPresentationWithoutChangingOffset()
    {
        WindowStore store = new();
        PanState state = new();
        state.SetMaxOffset(1000);
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        TestScrollInputSource source = new();
        QueuedDeltaScrollMotion easingMotion = new();
        using Scroller scroller = CreateScroller(state,
            store,
            mover,
            presentationSession,
            source: source,
            easingMotion: easingMotion);
        scroller.ScrollStarted += (_, _) => presentationSession.Begin();
        scroller.Start();

        source.RaiseScroll(-120);
        scroller.OnTick();

        Assert.True(presentationSession.IsActive);
        Assert.Equal(0, state.Offset);

        source.RaiseScroll(-120);
        scroller.OnTick();

        Assert.Equal(60, state.Offset);
    }

    private static Scroller CreateScroller(PanState state,
        WindowStore store,
        TestWindowMover mover,
        IScrollPresentationSession? presentationSession = null,
        IDeltaScrollMotion? pixelMotion = null,
        IScrollInputSource? source = null,
        IDeltaScrollMotion? easingMotion = null) =>
        new(state,
            presentationSession ?? new ScrollPresentationSession(),
            store,
            mover,
            new TestWindowConcealer(),
            new TestWindowMoveGuard(),
            new TestWindowDragGuard(),
            source ?? new TestScrollInputSource(),
            new TestDispatcher(),
            () => new ScrollerConfiguration { PixelsPerScrollNotch = 120 },
            pixelMotion ?? new TestDeltaScrollMotion(),
            easingMotion ?? new TestDeltaScrollMotion(),
            new TestVelocityScrollMotion(),
            () => { },
            () => { },
            NullLogger<Scroller>.Instance);

    private static TrackedWindow CreateWindow(int canvasX, bool isSticky, int handle = 1) => new()
    {
        Handle = new IntPtr(handle),
        CanvasX = canvasX,
        CanvasY = 100,
        Width = 800,
        Height = 600,
        IsSticky = isSticky,
        StickyViewportX = isSticky ? 250 : 0,
        LastPlacedX = int.MinValue,
        LastPlacedY = int.MinValue
    };

    private sealed class TestWindowMover :
        IWindowMover
    {
        public List<(IntPtr Handle, int X)> Moves { get; } = [];

        public void BeginBatch(int count)
        {
        }

        public void MoveTo(IntPtr windowHandle, int x, int y, int width, int height) =>
            Moves.Add((windowHandle, x));

        public void EndBatch()
        {
        }
    }

    private sealed class TestWindowConcealer :
        IWindowConcealer
    {
        public bool Conceal(IntPtr windowHandle) => true;

        public void Reveal(IntPtr windowHandle)
        {
        }

        public bool IsConcealed(IntPtr windowHandle) => false;

        public IReadOnlySet<IntPtr> ConcealedHandles() => new HashSet<IntPtr>();
    }

    private sealed class TestWindowMoveGuard :
        IWindowMoveGuard
    {
        public bool IsSystemMove => false;

        public WindowMoveScope Begin() => new(() => { });
    }

    private sealed class TestWindowDragGuard :
        IWindowDragGuard
    {
        public event Action? HoldStarted;

        public bool IsAnyDragging => false;

        public IntPtr DraggingWindow => IntPtr.Zero;

        public bool IsDragging(IntPtr windowHandle) => false;

        public void Start() => HoldStarted?.Invoke();

        public void Stop()
        {
        }
    }

    private sealed class TestScrollInputSource :
        IScrollInputSource
    {
        public event Action<int>? ScrollDeltaReceived;

        public event Action<double>? ScrollVelocityIdle;

        public event Action? MiddleButtonClicked;

        public void RaiseScroll(int delta) => ScrollDeltaReceived?.Invoke(delta);

        public void RaiseVelocity(double velocity) => ScrollVelocityIdle?.Invoke(velocity);

        public void RaiseMiddleButton() => MiddleButtonClicked?.Invoke();

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class TestDispatcher :
        IDispatcher
    {
        public void Dispatch(Action action) => action();
    }

    private sealed class TestDeltaScrollMotion :
        IDeltaScrollMotion
    {
        public bool IsActive => false;

        public void AddDelta(double pixels)
        {
        }

        public double Drain() => 0;

        public void Reset()
        {
        }
    }

    private sealed class QueuedDeltaScrollMotion(params double[] deltas) :
        IDeltaScrollMotion
    {
        private readonly Queue<double> deltas = new(deltas);

        public bool IsActive => deltas.Count > 0;

        public void AddDelta(double pixels) => deltas.Enqueue(pixels);

        public double Drain() => deltas.Count > 0 ? deltas.Dequeue() : 0;

        public void Reset() => deltas.Clear();
    }

    private sealed class TestVelocityScrollMotion :
        IVelocityScrollMotion
    {
        public bool IsActive => false;

        public void AddVelocity(double pixelsPerTick)
        {
        }

        public double Drain() => 0;

        public void Reset()
        {
        }
    }
}

public sealed class StickyWindowPagingTests
{
    [Fact]
    public void StickyWindowsDoNotIncreasePageCount()
    {
        WindowStore store = new();
        store.Add(CreateWindow(2500, false, 1));
        store.Add(CreateWindow(9250, true, 2));
        Pager pager = new(store,
            new PanState(),
            new TestScroller(),
            new TestWorkspace(),
            new TestForegroundWindowCoordinator(),
            NullLogger<Pager>.Instance);

        Assert.Equal(4, pager.PageCount);
    }

    [Fact]
    public void CurrentPageRemainsAvailableWhenAllWindowsAreSticky()
    {
        WindowStore store = new();
        store.Add(CreateWindow(5250, true, 1));
        PanState state = new();
        state.SetOffset(5000);
        Pager pager = new(store,
            state,
            new TestScroller(),
            new TestWorkspace(),
            new TestForegroundWindowCoordinator(),
            NullLogger<Pager>.Instance);

        Assert.Equal(6, pager.PageCount);
    }

    private static TrackedWindow CreateWindow(int canvasX, bool isSticky, int handle) => new()
    {
        Handle = new IntPtr(handle),
        CanvasX = canvasX,
        CanvasY = 100,
        Width = 800,
        Height = 600,
        IsSticky = isSticky,
        StickyViewportX = 250
    };

    private sealed class TestWorkspace :
        IWorkspace
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
}

internal sealed class TestScroller :
    IScroller
{
    public event EventHandler? ScrollStarted;

    public event EventHandler? ScrollStopped;

    public double VisualOffset { get; set; }

    public int RepositionCount { get; private set; }

    public int ResetCount { get; private set; }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public void CommitPresentation()
    {
    }

    public void OnTick()
    {
    }

    public void Reposition() => RepositionCount++;

    public void Reset()
    {
        ResetCount++;
    }

    public void ScrollBy(double delta)
    {
    }

    public void ScrollTo(double offset, bool animate = true) => VisualOffset = offset;

    public void Start() => ScrollStarted?.Invoke(this, EventArgs.Empty);

    public void Stop() => ScrollStopped?.Invoke(this, EventArgs.Empty);
}