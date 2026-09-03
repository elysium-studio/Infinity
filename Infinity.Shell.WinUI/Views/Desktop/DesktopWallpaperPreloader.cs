using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWallpaperPreloader(
    IDesktopBackgroundSource backgroundSource,
    DesktopWallpaperSurfaceProvider surfaceProvider,
    IDispatcher dispatcher,
    ILogger<DesktopWallpaperPreloader> logger)
{
    private volatile bool started;
    private int refreshQueued;

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        backgroundSource.BackgroundChanged += HandleBackgroundChanged;
        QueueRefresh();
    }

    public void Stop()
    {
        started = false;
        backgroundSource.BackgroundChanged -= HandleBackgroundChanged;
    }

    private void HandleBackgroundChanged(object? sender, EventArgs args) => QueueRefresh();

    private void QueueRefresh()
    {
        if (!started || Interlocked.Exchange(ref refreshQueued, 1) != 0)
        {
            return;
        }

        try
        {
            // LoadedImageSurface is created on the UI thread. Decoding is
            // asynchronous and never waits for the overview to open.
            dispatcher.Dispatch(Refresh);
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref refreshQueued, 0);
            logger.LogWarning(exception, "Could not queue wallpaper preloading");
        }
    }

    private void Refresh()
    {
        Interlocked.Exchange(ref refreshQueued, 0);

        if (!started)
        {
            return;
        }

        try
        {
            DesktopBackground background = backgroundSource.GetBackground();
            if (!string.IsNullOrWhiteSpace(background.Wallpaper))
            {
                _ = surfaceProvider.GetOrCreate(background);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not preload the desktop wallpaper");
        }
    }
}
