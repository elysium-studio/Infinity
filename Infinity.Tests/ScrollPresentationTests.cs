using Elysium.Application.Abstractions;
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
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, easingMotion: motion);
        scroller.OnTick();
        Assert.Empty(mover.Moves);
        Assert.Equal(100, state.Offset);
        scroller.OnTick();
        Assert.Empty(mover.Moves);
        Assert.Equal(200, state.Offset);
        Assert.True(presentationSession.IsActive);
        scroller.CommitPresentation();
        Assert.Collection(mover.Moves, move => Assert.Equal((window.Handle, 300), (move.Handle, move.X)));
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
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, source: source, easingMotion: easingMotion, navigationMotion: navigationMotion, pageCenterTargetResolver: new TestPageCenterTargetResolver(pageWidth: 1000));
        scroller.ScrollStarted += (_, _) => presentationSession.Begin();
        scroller.Start();
        source.RaiseScroll(-120);
        scroller.OnTick();
        Assert.True(presentationSession.IsActive);
        Assert.Equal(0, state.Offset);
        source.RaiseScroll(-120);
        scroller.OnTick();
        Assert.Equal(1000, state.Offset);
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
    public void StandardWheelInputRetargetsProgrammaticNavigationToTheAdjacentPage()
    {
        WindowStore store = new();
        PanState state = new();
        state.SetMaxOffset(1000);
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        TestScrollInputSource source = new();
        QueuedDeltaScrollMotion easingMotion = new();
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, source: source, easingMotion: easingMotion, navigationMotion: navigationMotion, pageCenterTargetResolver: new TestPageCenterTargetResolver(pageWidth: 1000));
        presentationSession.Begin();
        scroller.Start();
        scroller.ScrollTo(500);
        source.RaiseScroll(-120);
        scroller.OnTick();
        Assert.False(navigationMotion.IsActive);
        Assert.Equal(1000, state.Offset);
    }


    [Fact]
    public void StandardWheelDeltaNavigatesDirectlyToTheAdjacentPageCentre()
    {
        WindowStore store = new();
        PanState state = new();
        state.SetMaxOffset(2000);
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        presentationSession.Begin();
        TestScrollInputSource source = new();
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, source: source, navigationMotion: navigationMotion, pageCenterTargetResolver: new TestPageCenterTargetResolver(pageWidth: 1000));
        int stoppedCount = 0;
        scroller.ScrollStopped += (_, _) => stoppedCount++;
        scroller.Start();
        source.RaiseScroll(-120);
        scroller.OnTick();
        Assert.Equal(1000, state.Offset);
        Assert.False(navigationMotion.IsActive);
        Assert.Equal(1, stoppedCount);
    }


    [Fact]
    public void ReversingWheelDirectionTargetsThePreviousItemDuringAnimation()
    {
        WindowStore store = new();
        PanState state = new();
        state.SetMaxOffset(3000);
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        presentationSession.Begin();
        TestScrollInputSource source = new();
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, source: source, navigationMotion: navigationMotion, pageCenterTargetResolver: new TestPageCenterTargetResolver(pageWidth: 1000));
        scroller.Start();
        source.RaiseScroll(-120);
        source.RaiseScroll(120);
        scroller.OnTick();
        Assert.Equal(0, state.Offset);
    }


    [Fact]
    public void RepeatedWheelInputDoesNotResetTheRunningSpring()
    {
        PanState state = new();
        state.SetMaxOffset(5000);
        ScrollPresentationSession session = new();
        session.Begin();
        TestScrollInputSource source = new();
        AccumulatingDeltaScrollMotion motion = new();
        using Scroller scroller = CreateScroller(state, new WindowStore(), new TestWindowMover(), session, source: source, easingMotion: motion, pageCenterTargetResolver: new TestPageCenterTargetResolver(1000));
        scroller.Start();
        source.RaiseScroll(-120);
        int initialResets = motion.ResetCount;
        source.RaiseScroll(-120);
        source.RaiseScroll(-120);
        Assert.Equal(initialResets, motion.ResetCount);
        scroller.OnTick();
        Assert.Equal(3000, state.Offset);
    }


    [Fact]
    public void RapidWheelInputMovesOnEveryFrameAndSettlesAtThePageCentre()
    {
        PanState state = new();
        state.SetMaxOffset(200_000);
        ScrollPresentationSession session = new();
        session.Begin();
        TestScrollInputSource source = new();
        ManualScrollTimeProvider time = new();
        EasingScrollMotion motion = new(time);
        using Scroller scroller = CreateScroller(state, new WindowStore(), new TestWindowMover(), session, source: source, easingMotion: motion, pageCenterTargetResolver: new TestPageCenterTargetResolver(1000));
        scroller.Start();
        for (int frame = 0; frame < 12; frame++)
        {
            double previousOffset = state.Offset;
            for (int notch = 0; notch < 8; notch++)
            {
                time.Advance(TimeSpan.FromMilliseconds(2));
                source.RaiseScroll(-120);
            }

            scroller.OnTick();
            Assert.True(state.Offset > previousOffset);
        }

        Settle(scroller, motion, time);
        Assert.InRange(state.Offset, 96_000 - 0.5, 96_000 + 0.5);
    }


    [Fact]
    public void ReversalDropsQueuedForwardDestinationsAndMovesBack()
    {
        PanState state = new();
        state.SetMaxOffset(20_000);
        state.SetOffset(5000);
        ScrollPresentationSession session = new();
        session.Begin();
        TestScrollInputSource source = new();
        ManualScrollTimeProvider time = new();
        EasingScrollMotion motion = new(time);
        using Scroller scroller = CreateScroller(state, new WindowStore(), new TestWindowMover(), session, source: source, easingMotion: motion, pageCenterTargetResolver: new TestPageCenterTargetResolver(1000));
        scroller.Start();
        for (int notch = 0; notch < 5; notch++)
        {
            source.RaiseScroll(-120);
        }

        scroller.OnTick();
        double reversalOffset = state.Offset;
        double expectedTarget = (Math.Round(reversalOffset / 1000, MidpointRounding.AwayFromZero) - 1) * 1000;
        source.RaiseScroll(120);
        time.Advance(TimeSpan.FromMilliseconds(16));
        scroller.OnTick();
        Assert.True(state.Offset < reversalOffset);
        Settle(scroller, motion, time);
        Assert.InRange(state.Offset, expectedTarget - 0.5, expectedTarget + 0.5);
    }


    [Fact]
    public void CancelNavigationAlsoCancelsWheelMotionBeforeActivation()
    {
        PanState state = new();
        state.SetMaxOffset(3000);
        ScrollPresentationSession session = new();
        session.Begin();
        TestScrollInputSource source = new();
        AccumulatingDeltaScrollMotion motion = new();
        using Scroller scroller = CreateScroller(state, new WindowStore(), new TestWindowMover(), session, source: source, easingMotion: motion, pageCenterTargetResolver: new TestPageCenterTargetResolver(1000));
        scroller.Start();
        source.RaiseScroll(-120);
        scroller.CancelNavigation();
        scroller.OnTick();
        Assert.False(motion.IsActive);
        Assert.Equal(0, state.Offset);
    }


    [Fact]
    public void WheelTargetsAreClampedWithoutAddingAnInvisibleBacklog()
    {
        PanState state = new();
        state.SetMaxOffset(2000);
        ScrollPresentationSession session = new();
        session.Begin();
        TestScrollInputSource source = new();
        AccumulatingDeltaScrollMotion motion = new();
        using Scroller scroller = CreateScroller(state, new WindowStore(), new TestWindowMover(), session, source: source, easingMotion: motion, pageCenterTargetResolver: new TestPageCenterTargetResolver(1000));
        scroller.Start();
        for (int notch = 0; notch < 20; notch++)
        {
            source.RaiseScroll(-120);
        }

        scroller.OnTick();
        Assert.Equal(2000, state.Offset);
        source.RaiseScroll(120);
        scroller.OnTick();
        Assert.Equal(1000, state.Offset);
    }


    [Fact]
    public void ExplicitNavigationReplacesWheelMotionWithoutAFollowUpSnap()
    {
        PanState state = new();
        state.SetMaxOffset(3000);
        ScrollPresentationSession session = new();
        session.Begin();
        TestScrollInputSource source = new();
        AccumulatingDeltaScrollMotion wheelMotion = new();
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, new WindowStore(), new TestWindowMover(), session, source: source, easingMotion: wheelMotion, navigationMotion: navigationMotion, pageCenterTargetResolver: new TestPageCenterTargetResolver(1000));
        scroller.Start();
        source.RaiseScroll(-120);
        source.RaiseScroll(-120);
        scroller.ScrollTo(600);
        scroller.OnTick();
        scroller.OnTick();
        Assert.Equal(600, state.Offset);
        Assert.False(wheelMotion.IsActive);
        Assert.False(navigationMotion.IsActive);
    }


    private static void Settle(Scroller scroller, EasingScrollMotion motion, ManualScrollTimeProvider time)
    {
        for (int frame = 0; frame < 180 && motion.IsActive; frame++)
        {
            time.Advance(TimeSpan.FromMilliseconds(16));
            scroller.OnTick();
        }

        Assert.False(motion.IsActive);
    }


    [Fact]
    public void PrecisionGestureCentersAfterItsPixelMotionCompletes()
    {
        WindowStore store = new();
        PanState state = new();
        state.SetMaxOffset(2000);
        TestWindowMover mover = new();
        ScrollPresentationSession presentationSession = new();
        presentationSession.Begin();
        TestScrollInputSource source = new();
        QueuedDeltaScrollMotion easingMotion = new();
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, store, mover, presentationSession, source: source, easingMotion: easingMotion, navigationMotion: navigationMotion, pageCenterTargetResolver: new FixedPageCenterTargetResolver(1000));
        scroller.Start();
        source.RaiseScroll(-60);
        scroller.OnTick();
        Assert.Equal(30, state.Offset);
        Assert.True(navigationMotion.IsActive);
        scroller.OnTick();
        Assert.Equal(1000, state.Offset);
    }


    [Fact]
    public void ProgrammaticNavigationDoesNotStartFollowUpCentering()
    {
        WindowStore store = new();
        PanState state = new();
        state.SetMaxOffset(2000);
        TestWindowMover mover = new();
        QueuedDeltaScrollMotion navigationMotion = new();
        using Scroller scroller = CreateScroller(state, store, mover, navigationMotion: navigationMotion, pageCenterTargetResolver: new FixedPageCenterTargetResolver(1000));
        int stoppedCount = 0;
        scroller.ScrollStopped += (_, _) => stoppedCount++;
        scroller.ScrollTo(600);
        scroller.OnTick();
        Assert.Equal(600, state.Offset);
        Assert.False(navigationMotion.IsActive);
        Assert.Equal(1, stoppedCount);
    }


    private static Scroller CreateScroller(PanState state, WindowStore store, TestWindowMover mover, IScrollPresentationSession? presentationSession = null, IScrollInputSource? source = null, IDeltaScrollMotion? easingMotion = null, IDeltaScrollMotion? navigationMotion = null, IPageCenterTargetResolver? pageCenterTargetResolver = null) => new(state, presentationSession ?? new ScrollPresentationSession(), store, mover, new TestWindowMoveGuard(), new TestWindowDragGuard(), source ?? new TestScrollInputSource(), new TestDispatcher(), () => new ScrollerConfiguration { PixelsPerScrollNotch = 120 }, easingMotion ?? new AccumulatingDeltaScrollMotion(), navigationMotion ?? new TestDeltaScrollMotion(), new TestVelocityScrollMotion(), pageCenterTargetResolver ?? new TestPageCenterTargetResolver(), () =>  {  }, () =>  {  }, NullLogger<Scroller>.Instance);

    private static TrackedWindow CreateWindow(int canvasX, int handle = 1) => new()
    {
        Handle = new(handle),
        CanvasX = canvasX,
        CanvasY = 100,
        Width = 800,
        Height = 600,
        LastPlacedX = int.MinValue,
        LastPlacedY = int.MinValue
    };

    private sealed class TestWindowMover : IWindowMover
    {
        public List<(IntPtr Handle, int X)> Moves { get; } = [];

        public void BeginBatch(int count)
        {
        }


        public void MoveTo(IntPtr windowHandle, int x, int y, int width, int height) => Moves.Add((windowHandle, x));

        public void EndBatch()
        {
        }
    }


    private sealed class TestWindowMoveGuard : IWindowMoveGuard
    {
        public bool IsSystemMove => false;

        public WindowMoveScope Begin() => new(() =>  {  });
    }


    private sealed class TestWindowDragGuard : IWindowDragGuard
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


    private sealed class TestScrollInputSource : IScrollInputSource
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


    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }


    private sealed class TestDeltaScrollMotion : IDeltaScrollMotion
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


    private sealed class QueuedDeltaScrollMotion(params double[] deltas) : IDeltaScrollMotion
    {
        private readonly Queue<double> deltas = new(deltas);

        public bool IsActive => deltas.Count > 0;

        public void AddDelta(double pixels) => deltas.Enqueue(pixels);

        public double Drain() => deltas.Count > 0 ? deltas.Dequeue() : 0;

        public void Reset() => deltas.Clear();
    }


    private sealed class TestVelocityScrollMotion : IVelocityScrollMotion
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


    private sealed class TestPageCenterTargetResolver(double pageWidth = 0) : IPageCenterTargetResolver
    {
        public bool TryResolve(double offset, double minimumOffset, double maximumOffset, out double targetOffset)
        {
            targetOffset = offset;
            return false;
        }


        public bool TryResolveAdjacent(double offset, int pageDelta, double minimumOffset, double maximumOffset, out double targetOffset)
        {
            targetOffset = pageWidth > 0 ? Math.Clamp((Math.Round(offset / pageWidth, MidpointRounding.AwayFromZero) + pageDelta) * pageWidth, minimumOffset, maximumOffset) : offset;
            return pageWidth > 0 && Math.Abs(targetOffset - offset) >= 0.5;
        }
    }


    private sealed class FixedPageCenterTargetResolver(double target) : IPageCenterTargetResolver
    {
        public bool TryResolve(double offset, double minimumOffset, double maximumOffset, out double targetOffset)
        {
            targetOffset = Math.Clamp(target, minimumOffset, maximumOffset);
            return Math.Abs(targetOffset - offset) >= 0.5;
        }


        public bool TryResolveAdjacent(double offset, int pageDelta, double minimumOffset, double maximumOffset, out double targetOffset)
        {
            targetOffset = offset;
            return false;
        }
    }
}
