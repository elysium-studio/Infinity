using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class ScrollPresentationTests
{
    [Fact]
    public void PresentationSessionDefersWindowMovementUntilExplicitCommit()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(500);
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

    [Fact]
    public void ExplicitRepositionCommitsWindowDuringPresentation()
    {
        WindowStore store = new();
        TrackedWindow window = CreateWindow(500);
        store.Add(window);
        PanState state = new();
        state.SetOffset(200);
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        presentationSession.Begin();
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession);

        scroller.Reposition();

        Assert.Collection(mover.Moves, move => Assert.Equal((window.Handle, 300), (move.Handle, move.X)));
    }

    [Fact]
    public void WheelInputTakesControlFromProgrammaticNavigation()
    {
        WindowStore store = new();
        PanState state = new();
        state.SetMaxOffset(1000);
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        TestScrollInputSource source = new();
        QueuedDeltaScrollMotion easingMotion = new();
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, source: source, easingMotion: easingMotion, navigationMotion: navigationMotion);
        presentationSession.Begin();
        scroller.Start();
        scroller.ScrollTo(1000);

        source.RaiseScroll(-120);
        scroller.OnTick();

        Assert.False(navigationMotion.IsActive);
        Assert.Equal(60, state.Offset);
    }

    private static Scroller CreateScroller(PanState state,
        WindowStore store,
        TestWindowMover mover,
        IScrollPresentationSession? presentationSession = null,
        IDeltaScrollMotion? pixelMotion = null,
        IScrollInputSource? source = null,
        IDeltaScrollMotion? easingMotion = null,
        IDeltaScrollMotion? navigationMotion = null) =>
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
            navigationMotion ?? new TestDeltaScrollMotion(),
            new TestVelocityScrollMotion(),
            () => { },
            () => { },
            NullLogger<Scroller>.Instance);

    private static TrackedWindow CreateWindow(int canvasX, int handle = 1) => new()
    {
        Handle = new IntPtr(handle),
        CanvasX = canvasX,
        CanvasY = 100,
        Width = 800,
        Height = 600,
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
