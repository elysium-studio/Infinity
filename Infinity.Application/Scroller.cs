using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Infinity.Application;

public sealed class Scroller(IPanState state,
    IScrollPresentationSession presentationSession,
    IWindowStore store,
    IWindowMover mover,
    IWindowConcealer concealer,
    IWindowMoveGuard moveGuard,
    IWindowDragGuard dragGuard,
    IScrollInputSource source,
    IDispatcher dispatcher,
    Func<ScrollerConfiguration> configurationFactory,
    IDeltaScrollMotion pixelMotion,
    IDeltaScrollMotion easingMotion,
    IDeltaScrollMotion navigationMotion,
    IVelocityScrollMotion momentumMotion,
    IPageCenterTargetResolver pageCenterTargetResolver,
    Action startTimer,
    Action stopTimer,
    ILogger<Scroller> logger) :
    IScroller
{
    private const int StandardWheelDelta = 120;
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
    private bool isStarted;
    private bool isInputCenteringPending;
    private double? inputNavigationTarget;

    private WindowMoveScope? activeMoveScope;
    private Timer? moveGuardReleaseTimer;

    public event EventHandler? ScrollStarted;

    public event EventHandler? ScrollStopped;

    public double VisualOffset => state.Offset + springPosition;

    public void CancelNavigation()
    {
        navigationMotion.Reset();
        isInputCenteringPending = false;
        inputNavigationTarget = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    public void CommitPresentation()
    {
        if (!presentationSession.IsActive)
        {
            return;
        }

        try
        {
            RepositionWindows((int)Math.Round(state.Offset));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to reconcile window positions after scrolling");
        }
    }

    public void Reset()
    {
        pixelMotion.Reset();
        easingMotion.Reset();
        navigationMotion.Reset();
        momentumMotion.Reset();
        springPosition = 0;
        springVelocity = 0;
        isSpinging = false;
        isInputCenteringPending = false;
        inputNavigationTarget = null;
        haltRequested = false;
    }

    public void OnTick()
    {
        if (haltRequested)
        {
            pixelMotion.Reset();
            easingMotion.Reset();
            navigationMotion.Reset();
            momentumMotion.Reset();
            springPosition = 0;
            springVelocity = 0;
            isSpinging = false;
            isInputCenteringPending = false;
            inputNavigationTarget = null;
            haltRequested = false;
            CompleteScroll();
            return;
        }

        double delta = pixelMotion.Drain() + easingMotion.Drain() + navigationMotion.Drain() + momentumMotion.Drain();

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

        if (!presentationSession.IsActive)
        {
            RepositionWindows(intOffset);
        }

        if (!pixelMotion.IsActive && !easingMotion.IsActive && !navigationMotion.IsActive && !momentumMotion.IsActive && !isSpinging)
        {
            if (TryStartInputCentering())
            {
                return;
            }

            inputNavigationTarget = null;
            CompleteScroll();
        }
    }

    public void ScrollBy(double pixels)
    {
        if (pixels == 0)
        {
            return;
        }

        if (!IsMotionActive())
        {
            ScrollStarted?.Invoke(this, EventArgs.Empty);
        }

        haltRequested = false;
        isInputCenteringPending = false;
        inputNavigationTarget = null;
        pixelMotion.AddDelta(pixels);
        dispatcher.Dispatch(startTimer);
    }

    public void ScrollTo(double offset, bool animate = true)
    {
        isInputCenteringPending = false;
        inputNavigationTarget = null;
        double target = Math.Clamp(offset, state.MinOffset, state.MaxOffset);

        if (animate)
        {
            if (Math.Abs(target - state.Offset) < 0.01)
            {
                return;
            }

            if (!IsMotionActive())
            {
                ScrollStarted?.Invoke(this, EventArgs.Empty);
            }

            pixelMotion.Reset();
            easingMotion.Reset();
            momentumMotion.Reset();
            springPosition = 0;
            springVelocity = 0;
            isSpinging = false;
            haltRequested = false;
            navigationMotion.Reset();
            navigationMotion.AddDelta(target - state.Offset);
            dispatcher.Dispatch(startTimer);
        }
        else
        {
            pixelMotion.Reset();
            easingMotion.Reset();
            navigationMotion.Reset();
            momentumMotion.Reset();
            springPosition = 0;
            springVelocity = 0;
            isSpinging = false;
            haltRequested = false;
            state.SetOffset(target);
            if (!presentationSession.IsActive)
            {
                RepositionWindows((int)Math.Round(target));
            }
        }
    }

    public void Reposition()
    {
        RepositionWindows((int)Math.Round(VisualOffset));
    }

    public void Start()
    {
        if (isStarted)
        {
            return;
        }

        isStarted = true;
        source.ScrollDeltaReceived += HandleScrollDeltaReceived;
        source.ScrollVelocityIdle += HandleScrollVelocityIdle;
        dragGuard.HoldStarted += HandleHoldStarted;
    }

    public void Stop()
    {
        if (!isStarted)
        {
            return;
        }

        isStarted = false;
        source.ScrollDeltaReceived -= HandleScrollDeltaReceived;
        source.ScrollVelocityIdle -= HandleScrollVelocityIdle;
        dragGuard.HoldStarted -= HandleHoldStarted;

        stopTimer();

        if (presentationSession.IsActive)
        {
            try
            {
                RepositionWindows((int)Math.Round(state.Offset));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to reconcile window positions while stopping the scroller");
            }
            finally
            {
                presentationSession.End();
            }
        }

        moveGuardReleaseTimer?.Dispose();
        moveGuardReleaseTimer = null;

        activeMoveScope?.Dispose();
        activeMoveScope = null;
    }

    private void RepositionWindows(int intOffset)
    {
        activeMoveScope ??= moveGuard.Begin();

        bool anyDragging = dragGuard.IsAnyDragging;
        IReadOnlySet<nint> concealedHandles = concealer.ConcealedHandles();

        mover.BeginBatch(store.Count);

        try
        {
            foreach (TrackedWindow trackedWindow in store)
            {
                if (anyDragging && dragGuard.IsDragging(trackedWindow.Handle))
                {
                    continue;
                }

                int targetX = trackedWindow.CanvasX - intOffset;

                if (concealedHandles.Contains(trackedWindow.Handle))
                {
                    continue;
                }

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
        }
        finally
        {
            mover.EndBatch();
        }

        ScheduleMoveGuardRelease();
    }

    private void CompleteScroll()
    {
        stopTimer();
        ScrollStopped?.Invoke(this, EventArgs.Empty);
    }

    private bool IsMotionActive() =>
        pixelMotion.IsActive || easingMotion.IsActive || navigationMotion.IsActive || momentumMotion.IsActive || isSpinging;

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

    private void HandleMoveGuardReleaseTick(object? timerState) =>
        DispatchInputCallback(ReleaseMoveGuard, "move guard release");

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

        bool wasPresentationActive = presentationSession.IsActive;

        if (!IsMotionActive())
        {
            ScrollStarted?.Invoke(this, EventArgs.Empty);
        }

        if (!wasPresentationActive && presentationSession.IsActive)
        {
            DispatchInputCallback(startTimer, "scroll input");
            return;
        }

        if (nativeScrollDelta % StandardWheelDelta == 0)
        {
            NavigateByWheelDelta(nativeScrollDelta);
            return;
        }

        navigationMotion.Reset();
        inputNavigationTarget = null;
        isInputCenteringPending = true;

        double pixelsPerNotch = configurationFactory().PixelsPerScrollNotch;
        double pixels = (-nativeScrollDelta / 120.0) * pixelsPerNotch * WheelScrollScale;

        easingMotion.AddDelta(pixels);
        DispatchInputCallback(startTimer, "scroll input");
    }

    private void HandleScrollVelocityIdle(double velocity)
    {
        if (dragGuard.IsAnyDragging)
        {
            return;
        }

        if (!IsMotionActive())
        {
            ScrollStarted?.Invoke(this, EventArgs.Empty);
        }

        isInputCenteringPending = true;
        inputNavigationTarget = null;
        momentumMotion.AddVelocity(velocity);
        DispatchInputCallback(startTimer, "scroll momentum");
    }

    private void DispatchInputCallback(Action action, string operation)
    {
        try
        {
            dispatcher.Dispatch(action);
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            logger.LogDebug(exception, "The dispatcher rejected {Operation}", operation);
        }
    }

    private void HandleHoldStarted()
    {
        isInputCenteringPending = false;
        inputNavigationTarget = null;
        haltRequested = true;
    }

    private void NavigateByWheelDelta(int nativeScrollDelta)
    {
        int pageDelta = -Math.Sign(nativeScrollDelta) * Math.Max(1, Math.Abs(nativeScrollDelta) / StandardWheelDelta);
        double origin = inputNavigationTarget ?? state.Offset;

        if (!pageCenterTargetResolver.TryResolveAdjacent(origin, pageDelta, state.MinOffset, state.MaxOffset, out double targetOffset))
        {
            return;
        }

        pixelMotion.Reset();
        easingMotion.Reset();
        momentumMotion.Reset();
        navigationMotion.Reset();
        springPosition = 0;
        springVelocity = 0;
        isSpinging = false;
        isInputCenteringPending = false;
        inputNavigationTarget = targetOffset;
        haltRequested = false;
        navigationMotion.AddDelta(targetOffset - state.Offset);
        DispatchInputCallback(startTimer, "wheel page navigation");
    }

    private bool TryStartInputCentering()
    {
        if (!isInputCenteringPending)
        {
            return false;
        }

        isInputCenteringPending = false;

        if (!pageCenterTargetResolver.TryResolve(state.Offset, state.MinOffset, state.MaxOffset, out double targetOffset))
        {
            return false;
        }

        navigationMotion.Reset();
        navigationMotion.AddDelta(targetOffset - state.Offset);

        if (!navigationMotion.IsActive)
        {
            return false;
        }

        dispatcher.Dispatch(startTimer);
        return true;
    }
}
