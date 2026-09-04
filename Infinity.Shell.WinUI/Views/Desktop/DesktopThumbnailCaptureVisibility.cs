using Microsoft.UI.Dispatching;
using System;

namespace Infinity.Shell.WinUI;

/// <summary>Keeps capture lifetime separate from thumbnail layout and input.</summary>
internal sealed class DesktopThumbnailCaptureVisibility : IDisposable
{
    private readonly ThumbnailCompositionPreview? preview;
    private readonly DispatcherQueueTimer releaseTimer;
    private DesktopCaptureViewport viewport;
    private double x, y, width, height;
    private bool matchesFilter = true;
    private bool keepLive;
    private bool capturing;
    private bool transitionHeld;
    private bool disposed;

    public DesktopThumbnailCaptureVisibility(ThumbnailCompositionPreview? preview, DispatcherQueue dispatcher)
    {
        this.preview = preview;
        releaseTimer = dispatcher.CreateTimer();
        releaseTimer.IsRepeating = false;
        releaseTimer.Tick += HandleRelease;
    }

    public void SetViewport(DesktopCaptureViewport value)
    {
        if (viewport == value) return;
        viewport = value;
        Refresh();
    }

    public void HoldForTransition(TimeSpan? duration)
    {
        if (!capturing || duration is not { } delay || delay <= TimeSpan.Zero) return;
        releaseTimer.Stop();
        releaseTimer.Interval = delay;
        transitionHeld = true;
        releaseTimer.Start();
    }

    public void Update(double x, double y, double width, double height, bool matchesFilter, bool keepLive)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
        this.matchesFilter = matchesFilter;
        this.keepLive = keepLive;
        Refresh();
    }

    private void Refresh()
    {
        if (disposed) return;
        capturing = matchesFilter && (keepLive || transitionHeld ||
            viewport.ShouldCapture(x, y, width, height, capturing));
        preview?.Update(width, height, capturing);
    }

    private void HandleRelease(DispatcherQueueTimer sender, object args)
    {
        releaseTimer.Stop();
        transitionHeld = false;
        Refresh();
    }

    public void Dispose()
    {
        disposed = true;
        transitionHeld = false;
        releaseTimer.Stop();
        releaseTimer.Tick -= HandleRelease;
    }
}
