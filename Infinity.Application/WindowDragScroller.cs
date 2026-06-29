using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Infinity.Application;

public class WindowDragScroller(IPointerInputSource pointer,
    IModifierKeyState modifierKeyState,
    IWindowDragGuard dragGuard,
    IWorkspace workspace,
    IScroller scroller,
    IPanState state,
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
    private const int VirtualKeyLeftButton = 0x01;
    private const int VirtualKeyRightButton = 0x02;

    private readonly record struct CursorSample(int X, long TimestampMs);

    private readonly CursorSample[] velocitySamples = new CursorSample[VelocitySampleCount];
    private int velocitySampleIndex;
    private int velocitySampleCount;

    private CancellationTokenSource? scrollCancellation;
    private bool atBoundary;
    private long currentScrollAmountBits;
    private volatile int scrollDirection;
    private bool isDragging;

    public event Action? DragStarted;

    public event Action? DragMoved;

    public event Action? DragScrolled;

    public event Action? DragStopped;

    public bool IsAutoScrolling => scrollCancellation is not null;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    private static bool IsMouseButtonDown() =>
        IsKeyDown(VirtualKeyLeftButton) || IsKeyDown(VirtualKeyRightButton);

    private static bool IsKeyDown(int virtualKeyCode) =>
        (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;

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
        pointer.CursorMoved += HandleCursorMoved;
        modifierKeyState.StateChanged += HandleModifierStateChanged;
    }

    public void Stop()
    {
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

        int distanceFromRight = Math.Max(0, workspace.Width - x - SnapEdgePadding);
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

        logger.LogDebug("Drag started. IsAnyDragging={IsAnyDragging}, IsModifierActive={IsModifierActive}, MouseDown={MouseDown}", dragGuard.IsAnyDragging, modifierKeyState.IsActive, IsMouseButtonDown());

        DragStarted?.Invoke();
    }

    private void StopDragging()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;

        logger.LogDebug("Drag stopped. IsAnyDragging={IsAnyDragging}, IsModifierActive={IsModifierActive}, MouseDown={MouseDown}", dragGuard.IsAnyDragging, modifierKeyState.IsActive, IsMouseButtonDown());

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

        if (scrollCancellation is null)
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

        if (scrollCancellation is null)
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
        scrollCancellation = new CancellationTokenSource();
        CancellationToken token = scrollCancellation.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                double amount = BitConverter.Int64BitsToDouble(Interlocked.Read(ref currentScrollAmountBits)) * scrollDirection;
                double current = state.Offset;

                if (amount > 0 && current >= state.MaxOffset)
                {
                    scroller.Reset();
                    break;
                }

                if (amount < 0 && current <= state.MinOffset)
                {
                    scroller.Reset();
                    break;
                }

                double next = Math.Clamp(current + amount, state.MinOffset, state.MaxOffset);
                scroller.ScrollTo(next, animate: false);
                DragScrolled?.Invoke();

                await Task.Delay(ScrollIntervalMs, token);
            }
        }, token);
    }

    private void CancelScroll()
    {
        if (scrollCancellation is null)
        {
            return;
        }

        scrollCancellation.Cancel();
        scrollCancellation.Dispose();
        scrollCancellation = null;
        scroller.Reset();
    }
}