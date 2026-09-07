using System;
using System.Diagnostics;
using Infinity.Application.Abstractions;
using Microsoft.UI.Xaml.Media;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewDragScroller(
    IPanState panState,
    IScroller scroller,
    Func<DesktopOverviewDragScrollerConfiguration> configurationFactory,
    DesktopOverviewConfiguration overviewConfiguration) : IDisposable
{
    private const double EdgeThreshold = 160;
    private const double MinimumScrollAmount = 8;
    private const double MaximumScrollAmount = 40;
    private readonly DesktopDragScrollMotion windowMotion = new();
    private readonly long clockOrigin = Stopwatch.GetTimestamp();
    private bool isRendering;
    private TimeSpan lastFrameTime;
    private double scrollAmount;
    private int direction;
    private bool windowDrag;
    private double overviewScale = 1;
    private bool disposed;

    public event Action? ScrollLimitReached;

    public bool IsActive => isRendering;

    public void Update(double pointerX, double viewportWidth)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (windowDrag)
        {
            Stop();
        }

        if (!overviewConfiguration.IsEdgeScrollingEnabled)
        {
            Stop();
            return;
        }

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
        if (scrollAmount <= 0)
        {
            Stop();
            return;
        }

        StartRendering();
    }

    public void UpdateWindowDrag(double pointerX, double viewportWidth, double scale)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!overviewConfiguration.IsEdgeScrollingEnabled || !double.IsFinite(scale) || scale <= 0)
        {
            Stop();
            return;
        }

        if (!windowDrag)
        {
            Stop();
            windowDrag = true;
        }

        overviewScale = scale;
        windowMotion.Update(pointerX, viewportWidth, Stopwatch.GetElapsedTime(clockOrigin));
        StartRendering();
    }

    private void StartRendering()
    {
        if (isRendering)
        {
            return;
        }

        lastFrameTime = Stopwatch.GetElapsedTime(clockOrigin);
        isRendering = true;
        CompositionTarget.Rendering += HandleRendering;
    }

    private void StopRendering()
    {
        if (!isRendering)
        {
            return;
        }

        isRendering = false;
        CompositionTarget.Rendering -= HandleRendering;
    }


    public void Stop()
    {
        direction = 0;
        scrollAmount = 0;
        windowDrag = false;
        windowMotion.Reset();
        if (isRendering)
        {
            StopRendering();
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
        GC.SuppressFinalize(this);
    }


    private void HandleRendering(object? sender, object args)
    {
        if (!isRendering)
        {
            return;
        }

        if (!overviewConfiguration.IsEdgeScrollingEnabled)
        {
            Stop();
            return;
        }

        TimeSpan now = Stopwatch.GetElapsedTime(clockOrigin);
        double elapsed = Math.Clamp((now - lastFrameTime).TotalSeconds, 0, 0.032);
        lastFrameTime = now;
        if (elapsed == 0)
        {
            return;
        }

        double delta = windowDrag ? windowMotion.Advance(now, configurationFactory().SpeedLevel, overviewScale) : scrollAmount * direction * elapsed / 0.016;
        if (windowDrag && delta == 0)
        {
            if (!windowMotion.IsMoving)
            {
                StopRendering();
            }

            return;
        }

        double current = panState.Offset;
        double next = Math.Clamp(current + delta, panState.MinOffset, panState.MaxOffset);
        if (next == current)
        {
            Stop();
            ScrollLimitReached?.Invoke();
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
