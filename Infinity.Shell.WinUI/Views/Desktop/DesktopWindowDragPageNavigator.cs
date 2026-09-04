using Infinity.Application.Abstractions;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowDragPageNavigator(IPager pager,
    DesktopDragBoundaryCalculator boundaryCalculator,
    Func<DesktopOverviewDragScrollerConfiguration> configurationFactory,
    DesktopOverviewConfiguration overviewConfiguration) :
    IDisposable
{
    private const double EdgeThreshold = 96;
    private const double BoundaryTolerance = 2;
    private const double MinimumOutwardMovement = 0.5;
    private static readonly TimeSpan RepeatPullCooldown = TimeSpan.FromMilliseconds(140);

    private DispatcherQueueTimer? timer;
    private int direction;
    private int lastCommittedDirection;
    private long lastCommittedTimestamp;
    private bool disposed;

    public event Action? PageSnapCommitted;

    public bool IsEnabled => overviewConfiguration.IsEdgeScrollingEnabled;

    public void Update(DispatcherQueue dispatcherQueue, double pointerX, double horizontalPointerDelta, double viewportWidth, double overviewScale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!IsEnabled ||
            !double.IsFinite(pointerX) ||
            !double.IsFinite(viewportWidth) ||
            viewportWidth <= 0)
        {
            Reset();
            return;
        }

        (double minimumX, double maximumX) = boundaryCalculator.GetCenteredPageHorizontalBounds(viewportWidth, overviewScale);

        if (maximumX <= minimumX || pointerX < minimumX - BoundaryTolerance || pointerX > maximumX + BoundaryTolerance)
        {
            Reset();
            return;
        }

        pointerX = Math.Clamp(pointerX, minimumX, maximumX);

        double threshold = Math.Min(EdgeThreshold, (maximumX - minimumX) / 5);
        int nextDirection = pointerX <= minimumX + threshold
            ? -1
            : pointerX >= maximumX - threshold
                ? 1
                : 0;
        double pointerDelta = double.IsFinite(horizontalPointerDelta) ? horizontalPointerDelta : 0;

        if (nextDirection == 0)
        {
            lastCommittedDirection = 0;
            lastCommittedTimestamp = 0;
            StopTimer();
            return;
        }

        if (!CanNavigate(nextDirection))
        {
            StopTimer();
            return;
        }

        if (lastCommittedTimestamp != 0 && Stopwatch.GetElapsedTime(lastCommittedTimestamp) < RepeatPullCooldown)
        {
            StopTimer();
            return;
        }

        bool movingOutward = pointerDelta * nextDirection >= MinimumOutwardMovement;
        bool movingInward = pointerDelta * nextDirection <= -MinimumOutwardMovement;
        bool continuingPull = nextDirection == lastCommittedDirection &&
            (nextDirection < 0
                ? pointerX <= minimumX + BoundaryTolerance
                : pointerX >= maximumX - BoundaryTolerance);

        if (movingInward)
        {
            StopTimer();
            return;
        }

        if (timer?.IsRunning == true && direction == nextDirection)
        {
            return;
        }

        if (!movingOutward && !continuingPull)
        {
            StopTimer();
            return;
        }

        direction = nextDirection;
        timer ??= CreateTimer(dispatcherQueue);
        timer.Stop();
        timer.Interval = GetDwell(configurationFactory().SpeedLevel);
        timer.Start();
    }

    public void Stop() => Reset();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Reset();

        if (timer is not null)
        {
            timer.Tick -= HandleTimerTick;
            timer = null;
        }

        GC.SuppressFinalize(this);
    }

    private DispatcherQueueTimer CreateTimer(DispatcherQueue dispatcherQueue)
    {
        DispatcherQueueTimer result = dispatcherQueue.CreateTimer();
        result.IsRepeating = false;
        result.Tick += HandleTimerTick;
        return result;
    }

    private void HandleTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        int targetPage = pager.CurrentPage + direction;

        if (CanNavigate(direction))
        {
            lastCommittedDirection = direction;
            lastCommittedTimestamp = Stopwatch.GetTimestamp();
            pager.NavigateToPage(targetPage);
            PageSnapCommitted?.Invoke();
        }

        direction = 0;
    }

    private bool CanNavigate(int candidateDirection)
    {
        int targetPage = pager.CurrentPage + candidateDirection;
        return targetPage >= 0 && (!pager.MaxPages.HasValue || targetPage < pager.MaxPages.Value);
    }

    private void Reset()
    {
        StopTimer();
        lastCommittedDirection = 0;
        lastCommittedTimestamp = 0;
    }

    private void StopTimer()
    {
        direction = 0;

        if (timer?.IsRunning == true)
        {
            timer.Stop();
        }
    }

    private static TimeSpan GetDwell(DragScrollSpeed speed) => speed switch
    {
        DragScrollSpeed.Slow => TimeSpan.FromMilliseconds(450),
        DragScrollSpeed.Normal => TimeSpan.FromMilliseconds(320),
        DragScrollSpeed.Fast => TimeSpan.FromMilliseconds(220),
        DragScrollSpeed.Turbo => TimeSpan.FromMilliseconds(140),
        _ => TimeSpan.FromMilliseconds(320)
    };
}
