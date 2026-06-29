using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public class PointerInputSource :
    IPointerInputSource
{
    private const int IdleTimeoutMs = 120;
    private const int VelocitySampleCount = 5;
    private const int WheelDelta = 120;

    private readonly record struct DeltaSample(int Delta, long TimestampMs);

    private readonly IMouseInputSource mouseInputSource;
    private readonly IModifierKeyState modifierKeyState;
    private readonly Lock scrollGate = new();
    private readonly DeltaSample[] velocitySamples = new DeltaSample[VelocitySampleCount];
    private Timer? idleTimer;
    private int velocitySampleIndex;
    private int velocitySampleCount;
    private bool isPrecisionGesture;

    public event Action<int, int>? CursorMoved;
    public event Action? LeftButtonClicked;
    public event Action? MiddleButtonClicked;
    public event Action? RightButtonClicked;
    public event Action<int>? ScrollDeltaReceived;
    public event Action<double>? ScrollVelocityIdle;

    public PointerInputSource(IMouseInputSource mouseInputSource, IModifierKeyState modifierKeyState)
    {
        this.mouseInputSource = mouseInputSource;
        this.modifierKeyState = modifierKeyState;

        this.mouseInputSource.LeftButtonDown += HandleLeftButtonDown;
        this.mouseInputSource.MiddleButtonDown += HandleMiddleButtonDown;
        this.mouseInputSource.RightButtonDown += HandleRightButtonDown;
        this.mouseInputSource.MouseMoved += HandleMouseMoved;
        this.mouseInputSource.WheelScrolled += HandleWheelScrolled;
    }

    private void HandleLeftButtonDown() => LeftButtonClicked?.Invoke();

    private void HandleMiddleButtonDown() => MiddleButtonClicked?.Invoke();

    private void HandleRightButtonDown() => RightButtonClicked?.Invoke();

    private void HandleMouseMoved(object? sender, MouseMoveEventArgs args)
    {
        if (!modifierKeyState.IsActive)
        {
            return;
        }

        CursorMoved?.Invoke(args.X, args.Y);
    }

    private void HandleWheelScrolled(object? sender, MouseWheelEventArgs args)
    {
        if (!modifierKeyState.IsActive)
        {
            return;
        }

        args.Handled = true;

        if (args.Delta == 0)
        {
            return;
        }

        ScrollDeltaReceived?.Invoke(args.Delta);
        RecordVelocitySample(args.Delta);
    }

    private void RecordVelocitySample(int delta)
    {
        lock (scrollGate)
        {
            if (delta % WheelDelta != 0)
            {
                isPrecisionGesture = true;
            }

            velocitySamples[velocitySampleIndex] = new DeltaSample(delta, Environment.TickCount64);
            velocitySampleIndex = (velocitySampleIndex + 1) % VelocitySampleCount;

            if (velocitySampleCount < VelocitySampleCount)
            {
                velocitySampleCount++;
            }

            if (idleTimer is null)
            {
                idleTimer = new Timer(HandleIdleTimer, null, IdleTimeoutMs, Timeout.Infinite);
            }
            else
            {
                idleTimer.Change(IdleTimeoutMs, Timeout.Infinite);
            }
        }
    }

    private void HandleIdleTimer(object? state)
    {
        bool shouldFire;
        double velocity = 0;

        lock (scrollGate)
        {
            shouldFire = isPrecisionGesture;

            if (shouldFire)
            {
                velocity = ComputeVelocity();
            }

            velocitySampleCount = 0;
            velocitySampleIndex = 0;
            isPrecisionGesture = false;
        }

        if (shouldFire && velocity != 0)
        {
            ScrollVelocityIdle?.Invoke(velocity);
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

        long elapsedMs = velocitySamples[newestIndex].TimestampMs - velocitySamples[oldestIndex].TimestampMs;

        if (elapsedMs <= 0)
        {
            return 0.0;
        }

        double totalDelta = 0;

        for (int i = 0; i < velocitySampleCount; i++)
        {
            int index = (oldestIndex + i) % VelocitySampleCount;
            totalDelta += velocitySamples[index].Delta;
        }

        return totalDelta / elapsedMs;
    }

    public void Dispose()
    {
        mouseInputSource.LeftButtonDown -= HandleLeftButtonDown;
        mouseInputSource.MiddleButtonDown -= HandleMiddleButtonDown;
        mouseInputSource.RightButtonDown -= HandleRightButtonDown;
        mouseInputSource.MouseMoved -= HandleMouseMoved;
        mouseInputSource.WheelScrolled -= HandleWheelScrolled;

        lock (scrollGate)
        {
            idleTimer?.Dispose();
            idleTimer = null;
        }
    }
}