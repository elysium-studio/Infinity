using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public sealed class WindowDragScroller(IPointerInputSource pointer,
    IModifierKeyState modifierKeyState,
    IWindowDragGuard dragGuard,
    IWorkspace workspace,
    IScroller scroller,
    IPanState state,
    IDispatcher dispatcher,
    Func<WindowDragScrollerConfiguration> configurationFactory,
    ILogger<WindowDragScroller> logger) :
    IWindowDragScroller
{
    private const int EdgeThreshold = 200;
    private const int SnapEdgePadding = 64;
    private const int ScrollIntervalMs = 16;
    private const double MinScrollAmount = 5.0;
    private const double MaxScrollAmount = 60.0;
    private const double VelocityScale = 0.3;
    private const double DepthScale = 0.7;
    private const int VelocitySampleCount = 5;

    private readonly record struct CursorSample(int X, long TimestampMs);

    private readonly CursorSample[] velocitySamples = new CursorSample[VelocitySampleCount];
    private readonly Lock scrollLock = new();
    private int velocitySampleIndex;
    private int velocitySampleCount;

    private CancellationTokenSource? scrollCancellation;
    private int scrollGeneration;
    private bool atBoundary;
    private long currentScrollAmountBits;
    private volatile int scrollDirection;
    private bool isDragging;
    private bool isStarted;

    public event Action? DragStarted;

    public event Action? DragMoved;

    public event Action? DragScrolled;

    public event Action? DragStopped;

    public bool IsAutoScrolling
    {
        get
        {
            lock (scrollLock)
            {
                return scrollCancellation is not null;
            }
        }
    }

    private static double GetSpeedMultiplier(DragScrollSpeed speed) => speed switch
    {
        DragScrollSpeed.Slow => 0.5,
        DragScrollSpeed.Normal => 1.0,
        DragScrollSpeed.Fast => 2.0,
        DragScrollSpeed.Turbo => 3.5,
        _ => 1.0,
    };

    public void Start()
    {
        if (isStarted)
        {
            return;
        }

        isStarted = true;
        pointer.CursorMoved += HandleCursorMoved;
        modifierKeyState.StateChanged += HandleModifierStateChanged;
    }

    public void Stop()
    {
        if (!isStarted)
        {
            return;
        }

        isStarted = false;
        pointer.CursorMoved -= HandleCursorMoved;
        modifierKeyState.StateChanged -= HandleModifierStateChanged;
        StopDragging();
        CancelScroll();
    }

    private void HandleModifierStateChanged(bool isDown)
    {
        if (isDown)
        {
            return;
        }

        if (!isDragging)
        {
            return;
        }

        StopDragging();
        CancelScroll();
        atBoundary = false;
    }

    private void HandleCursorMoved(int x, int y)
    {
        RecordVelocitySample(x);

        bool modifierAndDrag = dragGuard.IsAnyDragging && modifierKeyState.IsActive;

        if (!modifierAndDrag)
        {
            StopDragging();
            CancelScroll();
            atBoundary = false;
            return;
        }

        StartDragging();
        DragMoved?.Invoke();

        int distanceFromRight = Math.Max(0, workspace.WorkAreaX + workspace.Width - x - SnapEdgePadding);
        int distanceFromLeft = Math.Max(0, x - workspace.WorkAreaX - SnapEdgePadding);

        if (distanceFromRight <= EdgeThreshold)
        {
            ScrollRight(distanceFromRight);
            return;
        }

        if (distanceFromLeft <= EdgeThreshold)
        {
            ScrollLeft(distanceFromLeft);
            return;
        }

        atBoundary = false;
        CancelScroll();
    }

    private void StartDragging()
    {
        if (isDragging)
        {
            return;
        }

        isDragging = true;

        logger.LogDebug("Drag started. IsAnyDragging={IsAnyDragging}, IsModifierActive={IsModifierActive}", dragGuard.IsAnyDragging, modifierKeyState.IsActive);

        DragStarted?.Invoke();
    }

    private void StopDragging()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;

        logger.LogDebug("Drag stopped. IsAnyDragging={IsAnyDragging}, IsModifierActive={IsModifierActive}", dragGuard.IsAnyDragging, modifierKeyState.IsActive);

        DragStopped?.Invoke();
    }

    private void ScrollRight(int distanceFromRight)
    {
        if (state.Offset >= state.MaxOffset)
        {
            StopAtBoundary();
            return;
        }

        atBoundary = false;
        Interlocked.Exchange(ref currentScrollAmountBits, BitConverter.DoubleToInt64Bits(ComputeScrollAmount(distanceFromRight)));
        scrollDirection = 1;

        if (!IsAutoScrolling)
        {
            StartScroll();
        }
    }

    private void ScrollLeft(int distanceFromLeft)
    {
        if (state.Offset <= state.MinOffset)
        {
            StopAtBoundary();
            return;
        }

        atBoundary = false;
        Interlocked.Exchange(ref currentScrollAmountBits, BitConverter.DoubleToInt64Bits(ComputeScrollAmount(distanceFromLeft)));
        scrollDirection = -1;

        if (!IsAutoScrolling)
        {
            StartScroll();
        }
    }

    private void StopAtBoundary()
    {
        if (atBoundary)
        {
            return;
        }

        atBoundary = true;
        CancelScroll();
    }

    private void RecordVelocitySample(int x)
    {
        velocitySamples[velocitySampleIndex] = new CursorSample(x, Environment.TickCount64);
        velocitySampleIndex = (velocitySampleIndex + 1) % VelocitySampleCount;

        if (velocitySampleCount < VelocitySampleCount)
        {
            velocitySampleCount++;
        }
    }

    private double ComputeVelocity()
    {
        if (velocitySampleCount < 2)
        {
            return 0.0;
        }

        int oldestIndex = (velocitySampleIndex - velocitySampleCount + VelocitySampleCount) % VelocitySampleCount;
        int newestIndex = (velocitySampleIndex - 1 + VelocitySampleCount) % VelocitySampleCount;

        CursorSample oldest = velocitySamples[oldestIndex];
        CursorSample newest = velocitySamples[newestIndex];

        long elapsedMs = newest.TimestampMs - oldest.TimestampMs;

        if (elapsedMs <= 0)
        {
            return 0.0;
        }

        return Math.Abs(newest.X - oldest.X) / (double)elapsedMs;
    }

    private double ComputeScrollAmount(int distanceFromEdge)
    {
        double depthFactor = 1.0 - (distanceFromEdge / (double)EdgeThreshold);
        double velocityFactor = Math.Min(ComputeVelocity(), 10.0) / 10.0;
        double combined = (depthFactor * DepthScale) + (velocityFactor * VelocityScale);
        double baseAmount = MinScrollAmount + (MaxScrollAmount - MinScrollAmount) * Math.Clamp(combined, 0.0, 1.0);

        return baseAmount * GetSpeedMultiplier(configurationFactory().SpeedLevel);
    }

    private void StartScroll()
    {
        lock (scrollLock)
        {
            if (scrollCancellation is not null)
            {
                return;
            }

            CancellationTokenSource cancellation = new();
            int generation = ++scrollGeneration;
            scrollCancellation = cancellation;
            _ = RunScrollLoopAsync(generation, cancellation);
        }
    }

    private void CancelScroll()
    {
        CancellationTokenSource? cancellation;

        lock (scrollLock)
        {
            cancellation = scrollCancellation;

            if (cancellation is null)
            {
                return;
            }

            scrollCancellation = null;
            scrollGeneration++;
        }

        cancellation.Cancel();
        scroller.Reset();
    }

    private async Task RunScrollLoopAsync(int generation, CancellationTokenSource cancellation)
    {
        try
        {
            while (true)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                dispatcher.Dispatch(() => ExecuteScrollTick(generation, cancellation));
                await Task.Delay(ScrollIntervalMs, cancellation.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Drag auto-scroll loop failed");
        }
        finally
        {
            lock (scrollLock)
            {
                if (ReferenceEquals(scrollCancellation, cancellation))
                {
                    scrollCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void ExecuteScrollTick(int generation, CancellationTokenSource cancellation)
    {
        lock (scrollLock)
        {
            if (generation != scrollGeneration || !ReferenceEquals(scrollCancellation, cancellation) || cancellation.IsCancellationRequested)
            {
                return;
            }
        }

        try
        {
            double amount = BitConverter.Int64BitsToDouble(Interlocked.Read(ref currentScrollAmountBits)) * scrollDirection;
            double current = state.Offset;

            if ((amount > 0 && current >= state.MaxOffset) ||
                (amount < 0 && current <= state.MinOffset))
            {
                CancelScroll();
                return;
            }

            double next = Math.Clamp(current + amount, state.MinOffset, state.MaxOffset);
            scroller.ScrollTo(next, animate: false);
            DragScrolled?.Invoke();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Drag auto-scroll tick failed");
            CancelScroll();
        }
    }
}
