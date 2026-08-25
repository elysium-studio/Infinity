using Infinity.Application.Abstractions;
using Microsoft.UI.Dispatching;
using System;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewDragScroller(IPanState panState,
    IScroller scroller,
    Func<WindowDragScrollerConfiguration> configurationFactory) :
    IDisposable
{
    private const double EdgeThreshold = 160;
    private const double MinimumScrollAmount = 8;
    private const double MaximumScrollAmount = 40;
    private static readonly TimeSpan ScrollInterval = TimeSpan.FromMilliseconds(16);

    private DispatcherQueueTimer? timer;
    private double scrollAmount;
    private int direction;
    private bool disposed;

    public void Update(DispatcherQueue dispatcherQueue, double pointerX, double viewportWidth)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!double.IsFinite(pointerX) || !double.IsFinite(viewportWidth) || viewportWidth <= 0)
        {
            Stop();
            return;
        }

        double threshold = Math.Min(EdgeThreshold, viewportWidth / 4);
        int nextDirection;
        double distanceFromEdge;

        if (pointerX <= threshold)
        {
            nextDirection = -1;
            distanceFromEdge = Math.Max(0, pointerX);
        }
        else if (pointerX >= viewportWidth - threshold)
        {
            nextDirection = 1;
            distanceFromEdge = Math.Max(0, viewportWidth - pointerX);
        }
        else
        {
            Stop();
            return;
        }

        direction = nextDirection;
        double depth = 1 - Math.Clamp(distanceFromEdge / threshold, 0, 1);
        double baseAmount = MinimumScrollAmount + ((MaximumScrollAmount - MinimumScrollAmount) * depth);
        scrollAmount = baseAmount * GetSpeedMultiplier(configurationFactory().SpeedLevel);

        if (timer is null)
        {
            timer = dispatcherQueue.CreateTimer();
            timer.Interval = ScrollInterval;
            timer.IsRepeating = true;
            timer.Tick += HandleTick;
        }

        if (!timer.IsRunning)
        {
            timer.Start();
        }
    }

    public void Stop()
    {
        direction = 0;
        scrollAmount = 0;

        if (timer?.IsRunning == true)
        {
            timer.Stop();
            scroller.Reset();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();

        if (timer is not null)
        {
            timer.Tick -= HandleTick;
            timer = null;
        }

        GC.SuppressFinalize(this);
    }

    private void HandleTick(DispatcherQueueTimer sender, object args)
    {
        double current = panState.Offset;
        double next = Math.Clamp(current + (scrollAmount * direction),
            panState.MinOffset,
            panState.MaxOffset);

        if (next == current)
        {
            Stop();
            return;
        }

        scroller.ScrollTo(next, animate: false);
    }

    private static double GetSpeedMultiplier(DragScrollSpeed speed) => speed switch
    {
        DragScrollSpeed.Slow => 0.5,
        DragScrollSpeed.Normal => 1,
        DragScrollSpeed.Fast => 2,
        DragScrollSpeed.Turbo => 3.5,
        _ => 1
    };
}
