using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Infinity.Application;

public class Scroller(IPanState state,
    IWindowStore store,
    IWindowMover mover,
    IWindowMoveGuard moveGuard,
    IWindowDragGuard dragGuard,
    IScrollInputSource source,
    IDispatcher dispatcher,
    Func<ScrollerConfiguration> configurationFactory,
    IDeltaScrollMotion pixelMotion,
    IDeltaScrollMotion easingMotion,
    IVelocityScrollMotion momentumMotion,
    Action startTimer,
    Action stopTimer,
    ILogger<Scroller> logger) :
    IScroller
{
    private const double WheelScrollScale = 0.50;

    private const double SpringStiffness = 0.35;
    private const double SpringDamping = 0.60;
    private const double SpringStopThreshold = 0.1;
    private const double SpringImpulseScale = 0.18;
    private const double SpringMaxVelocity = 25.0;

    private const int SystemMoveGraceMilliseconds = 250;

    private volatile bool haltRequested;
    private double springPosition;
    private double springVelocity;
    private bool isSpinging;

    private WindowMoveScope? activeMoveScope;
    private Timer? moveGuardReleaseTimer;

    public event EventHandler? ScrollStarted;
    public event EventHandler? ScrollStopped;

    public double VisualOffset => state.Offset + springPosition;

    public void Dispose() => Stop();

    public void Reset()
    {
        pixelMotion.Reset();
        easingMotion.Reset();
        momentumMotion.Reset();
        springPosition = 0;
        springVelocity = 0;
        isSpinging = false;
        haltRequested = false;
    }

    public void OnTick()
    {
        if (haltRequested)
        {
            pixelMotion.Reset();
            easingMotion.Reset();
            momentumMotion.Reset();
            springPosition = 0;
            springVelocity = 0;
            isSpinging = false;
            haltRequested = false;
            stopTimer();
            ScrollStopped?.Invoke(this, EventArgs.Empty);
            return;
        }

        double delta = pixelMotion.Drain() + easingMotion.Drain() + momentumMotion.Drain();

        if (Math.Abs(delta) > 0.01)
        {
            double offsetBefore = state.Offset;
            state.ApplyDelta(delta);

            if (state.Offset == offsetBefore)
            {
                double impulse = Math.Clamp(delta * SpringImpulseScale, -SpringMaxVelocity, SpringMaxVelocity);

                if (!isSpinging)
                {
                    isSpinging = true;
                    springPosition = 0;
                    springVelocity = impulse;
                }
                else
                {
                    springVelocity = Math.Clamp(springVelocity + impulse, -SpringMaxVelocity, SpringMaxVelocity);
                }

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Boundary hit. Offset={Offset} Min={Min} Max={Max} Delta={Delta} Impulse={Impulse} SpringVelocity={SpringVelocity}", state.Offset, state.MinOffset, state.MaxOffset, delta, impulse, springVelocity);
                }
            }
        }

        if (isSpinging)
        {
            double springForce = -SpringStiffness * springPosition;
            springVelocity = (springVelocity + springForce) * SpringDamping;
            springPosition += springVelocity;

            if (state.Offset <= state.MinOffset)
            {
                springPosition = Math.Min(springPosition, 0);
            }
            else if (state.Offset >= state.MaxOffset)
            {
                springPosition = Math.Max(springPosition, 0);
            }

            if (Math.Abs(springPosition) < SpringStopThreshold && Math.Abs(springVelocity) < SpringStopThreshold)
            {
                springPosition = 0;
                springVelocity = 0;
                isSpinging = false;
            }
        }

        double exactOffset = state.Offset + springPosition;
        int intOffset = (int)Math.Round(exactOffset);

        RepositionWindows(intOffset);

        if (!pixelMotion.IsActive && !easingMotion.IsActive && !momentumMotion.IsActive && !isSpinging)
        {
            stopTimer();
            ScrollStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ScrollBy(double pixels)
    {
        haltRequested = false;
        pixelMotion.AddDelta(pixels);
        dispatcher.Dispatch(startTimer);
    }

    public void ScrollTo(double offset, bool animate = true)
    {
        double target = Math.Clamp(offset, state.MinOffset, state.MaxOffset);

        if (animate)
        {
            haltRequested = false;
            easingMotion.Reset();
            easingMotion.AddDelta(target - state.Offset);
            dispatcher.Dispatch(startTimer);
        }
        else
        {
            pixelMotion.Reset();
            easingMotion.Reset();
            momentumMotion.Reset();
            springPosition = 0;
            springVelocity = 0;
            isSpinging = false;
            haltRequested = false;
            state.SetOffset(target);
            RepositionWindows((int)Math.Round(target));
        }
    }

    public void Start()
    {
        source.ScrollDeltaReceived += HandleScrollDeltaReceived;
        source.ScrollVelocityIdle += HandleScrollVelocityIdle;
        dragGuard.HoldStarted += HandleHoldStarted;
    }

    public void Stop()
    {
        source.ScrollDeltaReceived -= HandleScrollDeltaReceived;
        source.ScrollVelocityIdle -= HandleScrollVelocityIdle;
        dragGuard.HoldStarted -= HandleHoldStarted;

        stopTimer();

        moveGuardReleaseTimer?.Dispose();
        moveGuardReleaseTimer = null;

        activeMoveScope?.Dispose();
        activeMoveScope = null;
    }

    private void RepositionWindows(int intOffset)
    {
        activeMoveScope ??= moveGuard.Begin();

        bool anyDragging = dragGuard.IsAnyDragging;

        mover.BeginBatch(store.Count);

        foreach (TrackedWindow trackedWindow in store)
        {
            if (anyDragging && dragGuard.IsDragging(trackedWindow.Handle))
            {
                continue;
            }

            int targetX = trackedWindow.CanvasX - intOffset;
            int targetY = trackedWindow.CanvasY;

            if (trackedWindow.LastPlacedX == targetX &&
                trackedWindow.LastPlacedY == targetY)
            {
                continue;
            }

            mover.MoveTo(trackedWindow.Handle, targetX, targetY, trackedWindow.Width, trackedWindow.Height);
            trackedWindow.LastPlacedX = targetX;
            trackedWindow.LastPlacedY = targetY;
        }

        mover.EndBatch();

        ScheduleMoveGuardRelease();
    }

    private void ScheduleMoveGuardRelease()
    {
        if (moveGuardReleaseTimer is null)
        {
            moveGuardReleaseTimer = new Timer(HandleMoveGuardReleaseTick, null, SystemMoveGraceMilliseconds, Timeout.Infinite);
        }
        else
        {
            moveGuardReleaseTimer.Change(SystemMoveGraceMilliseconds, Timeout.Infinite);
        }
    }

    private void HandleMoveGuardReleaseTick(object? timerState) => dispatcher.Dispatch(ReleaseMoveGuard);

    private void ReleaseMoveGuard()
    {
        activeMoveScope?.Dispose();
        activeMoveScope = null;
    }

    private void HandleScrollDeltaReceived(int nativeScrollDelta)
    {
        if (dragGuard.IsAnyDragging)
        {
            return;
        }

        if (!pixelMotion.IsActive && !easingMotion.IsActive && !momentumMotion.IsActive && !isSpinging)
        {
            ScrollStarted?.Invoke(this, EventArgs.Empty);
        }

        double pixelsPerNotch = configurationFactory().PixelsPerScrollNotch;
        double pixels = (-nativeScrollDelta / 120.0) * pixelsPerNotch * WheelScrollScale;

        easingMotion.AddDelta(pixels);
        dispatcher.Dispatch(startTimer);
    }

    private void HandleScrollVelocityIdle(double velocity)
    {
        if (dragGuard.IsAnyDragging)
        {
            return;
        }

        if (!pixelMotion.IsActive && !easingMotion.IsActive && !momentumMotion.IsActive && !isSpinging)
        {
            ScrollStarted?.Invoke(this, EventArgs.Empty);
        }

        momentumMotion.AddVelocity(velocity);
        dispatcher.Dispatch(startTimer);
    }

    private void HandleHoldStarted()
    {
        haltRequested = true;
    }
}