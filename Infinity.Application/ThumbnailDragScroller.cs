using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public sealed class ThumbnailDragScroller :
    IThumbnailDragScroller
{
    private const double EdgeTolerance = 2.0;
    private const double ScrollAmount = 24.0;
    private const int ScrollIntervalMilliseconds = 16;

    private readonly Lock scrollLock = new();
    private readonly IModifierKeyState modifierKeyState;
    private readonly IScroller scroller;
    private readonly IPanState state;
    private readonly IDispatcher dispatcher;
    private readonly Func<WindowDragScrollerConfiguration> configurationFactory;
    private readonly ILogger<ThumbnailDragScroller> logger;
    private Timer? scrollTimer;
    private IntPtr activeWindow;
    private int direction;
    private int scrollGeneration;
    private int tickPending;
    private bool isDisposed;

    public event Action? Scrolled;

    public bool IsScrolling
    {
        get
        {
            lock (scrollLock)
            {
                return scrollTimer is not null;
            }
        }
    }

    public ThumbnailDragScroller(IModifierKeyState modifierKeyState,
        IScroller scroller,
        IPanState state,
        IDispatcher dispatcher,
        Func<WindowDragScrollerConfiguration> configurationFactory,
        ILogger<ThumbnailDragScroller> logger)
    {
        this.modifierKeyState = modifierKeyState;
        this.scroller = scroller;
        this.state = state;
        this.dispatcher = dispatcher;
        this.configurationFactory = configurationFactory;
        this.logger = logger;
        modifierKeyState.StateChanged += HandleModifierStateChanged;
    }

    public bool Begin(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        lock (scrollLock)
        {
            if (isDisposed || activeWindow != IntPtr.Zero)
            {
                return false;
            }

            activeWindow = windowHandle;
            direction = 0;
            return true;
        }
    }

    public void Update(IntPtr windowHandle, double pointerX, double viewportWidth)
    {
        int nextDirection = ResolveDirection(pointerX, viewportWidth);

        lock (scrollLock)
        {
            if (isDisposed || activeWindow != windowHandle)
            {
                return;
            }

            direction = nextDirection;
        }

        RefreshTimer();
    }

    public void End(IntPtr windowHandle)
    {
        lock (scrollLock)
        {
            if (activeWindow != windowHandle)
            {
                return;
            }

            activeWindow = IntPtr.Zero;
            direction = 0;
        }

        RefreshTimer();
    }

    public void Dispose()
    {
        Timer? timer;

        lock (scrollLock)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            activeWindow = IntPtr.Zero;
            direction = 0;
            timer = scrollTimer;
            scrollTimer = null;
            scrollGeneration++;
        }

        modifierKeyState.StateChanged -= HandleModifierStateChanged;
        timer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void HandleModifierStateChanged(bool isActive) => RefreshTimer();

    private void RefreshTimer()
    {
        Timer? timerToDispose = null;
        bool modifierActive = modifierKeyState.IsActive;

        lock (scrollLock)
        {
            bool shouldScroll = !isDisposed &&
                activeWindow != IntPtr.Zero &&
                direction != 0 &&
                modifierActive;

            if (shouldScroll && scrollTimer is null)
            {
                int generation = ++scrollGeneration;
                Timer timer = new(HandleScrollTimerTick,
                    generation,
                    Timeout.Infinite,
                    Timeout.Infinite);
                scrollTimer = timer;
                timer.Change(0, ScrollIntervalMilliseconds);
            }
            else if (!shouldScroll && scrollTimer is not null)
            {
                timerToDispose = scrollTimer;
                scrollTimer = null;
                scrollGeneration++;
            }
        }

        timerToDispose?.Dispose();
    }

    private void HandleScrollTimerTick(object? state)
    {
        if (state is not int generation || Interlocked.Exchange(ref tickPending, 1) != 0)
        {
            return;
        }

        try
        {
            dispatcher.Dispatch(() =>
            {
                try
                {
                    ExecuteScrollTick(generation);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Thumbnail drag-scroll tick failed");
                    StopTimer(generation);
                }
                finally
                {
                    Volatile.Write(ref tickPending, 0);
                }
            });
        }
        catch (Exception exception)
        {
            Volatile.Write(ref tickPending, 0);
            logger.LogError(exception, "Failed to dispatch thumbnail drag-scroll tick");
            StopTimer(generation);
        }
    }

    private void ExecuteScrollTick(int generation)
    {
        int currentDirection;

        lock (scrollLock)
        {
            if (generation != scrollGeneration ||
                scrollTimer is null ||
                activeWindow == IntPtr.Zero)
            {
                return;
            }

            currentDirection = direction;
        }

        if (currentDirection == 0 || !modifierKeyState.IsActive)
        {
            StopTimer(generation);
            return;
        }

        double currentOffset = state.Offset;
        double amount = ScrollAmount * GetSpeedMultiplier(configurationFactory().SpeedLevel) * currentDirection;
        double nextOffset = Math.Clamp(currentOffset + amount, state.MinOffset, state.MaxOffset);

        if (nextOffset == currentOffset)
        {
            StopTimer(generation);
            return;
        }

        scroller.ScrollTo(nextOffset, animate: false);
        Scrolled?.Invoke();
    }

    private void StopTimer(int generation)
    {
        Timer? timer = null;

        lock (scrollLock)
        {
            if (generation == scrollGeneration && scrollTimer is not null)
            {
                timer = scrollTimer;
                scrollTimer = null;
                scrollGeneration++;
            }
        }

        timer?.Dispose();
    }

    private static int ResolveDirection(double pointerX, double viewportWidth)
    {
        if (!double.IsFinite(pointerX) ||
            !double.IsFinite(viewportWidth) ||
            viewportWidth <= EdgeTolerance * 2)
        {
            return 0;
        }

        if (pointerX <= EdgeTolerance)
        {
            return -1;
        }

        return pointerX >= viewportWidth - EdgeTolerance ? 1 : 0;
    }

    private static double GetSpeedMultiplier(DragScrollSpeed speed) => speed switch
    {
        DragScrollSpeed.Slow => 0.5,
        DragScrollSpeed.Normal => 1.0,
        DragScrollSpeed.Fast => 2.0,
        DragScrollSpeed.Turbo => 3.5,
        _ => 1.0
    };
}
