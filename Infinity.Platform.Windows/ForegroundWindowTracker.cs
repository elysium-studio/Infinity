using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public class ForegroundWindowTracker(IWindowEventListener listener,
    IWindowFocusGuard focusGuard,
    IDispatcher dispatcher,
    ILogger<ForegroundWindowTracker> logger) :
    IForegroundWindowTracker
{
    private static readonly TimeSpan ForegroundDelay = TimeSpan.FromMilliseconds(50);

    private int isStarted;
    private int foregroundInFlight;
    private int foregroundRequested;
    private nint pendingForegroundWindowHandle;

    public event EventHandler<nint>? ForegroundWindowChanged;

    public void Start()
    {
        if (Interlocked.CompareExchange(ref isStarted, 1, 0) != 0)
        {
            return;
        }

        listener.ForegroundChanged += HandleForegroundChanged;
    }

    public void Stop()
    {
        if (Interlocked.CompareExchange(ref isStarted, 0, 1) != 1)
        {
            return;
        }

        listener.ForegroundChanged -= HandleForegroundChanged;

        Volatile.Write(ref foregroundRequested, 0);
        Interlocked.Exchange(ref pendingForegroundWindowHandle, 0);
    }

    public void NotifyForegroundWindowChanged(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        RaiseForegroundWindowChanged(windowHandle, false);
    }

    private void HandleForegroundChanged(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        Interlocked.Exchange(ref pendingForegroundWindowHandle, windowHandle);
        ScheduleForegroundPublish();
    }

    private void ScheduleForegroundPublish()
    {
        if (!IsStarted())
        {
            return;
        }

        Volatile.Write(ref foregroundRequested, 1);

        if (Interlocked.CompareExchange(ref foregroundInFlight, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(RunForegroundPublishLoopAsync);
    }

    private async Task RunForegroundPublishLoopAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(ForegroundDelay).ConfigureAwait(false);

                Volatile.Write(ref foregroundRequested, 0);

                if (IsStarted())
                {
                    PublishPendingForegroundWindow();
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Exception in foreground window tracker loop");
            }

            Interlocked.Exchange(ref foregroundInFlight, 0);

            if (!IsStarted())
            {
                return;
            }

            if (Volatile.Read(ref foregroundRequested) == 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref foregroundInFlight, 1, 0) != 0)
            {
                return;
            }
        }
    }

    private void PublishPendingForegroundWindow()
    {
        nint windowHandle = Interlocked.Exchange(ref pendingForegroundWindowHandle, 0);

        if (windowHandle == 0)
        {
            return;
        }

        if (!IsWindowHandleValid(windowHandle))
        {
            return;
        }

        if (IsDirectFocus(windowHandle))
        {
            return;
        }

        RaiseForegroundWindowChanged(windowHandle, true);
    }

    private void RaiseForegroundWindowChanged(nint windowHandle, bool requireStarted)
    {
        EventHandler<nint>? handler = ForegroundWindowChanged;

        if (handler is null)
        {
            return;
        }

        Dispatch(() =>
        {
            if (requireStarted && !IsStarted())
            {
                return;
            }

            handler(this, windowHandle);
        });
    }

    private bool IsDirectFocus(nint windowHandle)
    {
        try
        {
            return focusGuard.IsDirect(windowHandle);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Focus guard failed while checking direct foreground window: {WindowHandle}", windowHandle);
            return false;
        }
    }

    private void Dispatch(Action action)
    {
        try
        {
            dispatcher.Dispatch(action);
        }
        catch (ObjectDisposedException exception)
        {
            logger.LogDebug(exception, "Dispatcher was disposed while publishing foreground window state");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogDebug(exception, "Dispatcher rejected foreground window state publication");
        }
    }

    private bool IsStarted() => Volatile.Read(ref isStarted) != 0;

    private static bool IsWindowHandleValid(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        return PInvoke.IsWindow(new HWND(windowHandle));
    }
}