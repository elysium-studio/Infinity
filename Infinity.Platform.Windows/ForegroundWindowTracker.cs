using System.Runtime.InteropServices;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public sealed class ForegroundWindowTracker : IForegroundWindowTracker
{
    private static readonly TimeSpan ForegroundDelay = TimeSpan.FromMilliseconds(50);
    private readonly IWindowEventListener listener;
    private readonly IWindowFocusGuard focusGuard;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<ForegroundWindowTracker> logger;
    private readonly TimeSpan foregroundDelay;
    private readonly Func<nint, bool> isWindowHandleValid;
    private readonly Lock transitionSyncRoot = new();
    private readonly ForegroundTransitionHistory transitionHistory = new();
    private int isStarted;
    private int foregroundInFlight;
    private int foregroundRequested;

    public event EventHandler<nint>? ForegroundWindowChanged;

    public ForegroundWindowTracker(IWindowEventListener listener, IWindowFocusGuard focusGuard, IDispatcher dispatcher, ILogger<ForegroundWindowTracker> logger) : this(listener, focusGuard, dispatcher, logger, ForegroundDelay, IsWindowHandleValid)
    {
    }


    internal ForegroundWindowTracker(IWindowEventListener listener, IWindowFocusGuard focusGuard, IDispatcher dispatcher, ILogger<ForegroundWindowTracker> logger, TimeSpan foregroundDelay, Func<nint, bool> isWindowHandleValid)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(foregroundDelay, TimeSpan.Zero);
        this.listener = listener;
        this.focusGuard = focusGuard;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.foregroundDelay = foregroundDelay;
        this.isWindowHandleValid = isWindowHandleValid;
    }


    public void Start()
    {
        if (Interlocked.CompareExchange(ref isStarted, 1, 0) != 0)
        {
            return;
        }

        listener.ForegroundChanged += HandleForegroundChanged;
        listener.WindowDestroyed += HandleWindowDestroyed;
    }


    public void Stop()
    {
        if (Interlocked.CompareExchange(ref isStarted, 0, 1) != 1)
        {
            return;
        }

        listener.ForegroundChanged -= HandleForegroundChanged;
        listener.WindowDestroyed -= HandleWindowDestroyed;
        Volatile.Write(ref foregroundRequested, 0);
        lock (transitionSyncRoot)
        {
            transitionHistory.Reset();
        }
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

        lock (transitionSyncRoot)
        {
            transitionHistory.RecordForeground(windowHandle);
        }

        ScheduleForegroundPublish();
    }


    private void HandleWindowDestroyed(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        lock (transitionSyncRoot)
        {
            transitionHistory.RecordDestroyed(windowHandle);
        }
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
                await Task.Delay(foregroundDelay).ConfigureAwait(false);
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
        ForegroundTransition transition;
        lock (transitionSyncRoot)
        {
            transition = transitionHistory.TakePending();
        }

        nint windowHandle = transition.WindowHandle;
        if (windowHandle == 0)
        {
            return;
        }

        if (transition.IsCloseFallback)
        {
            logger.LogDebug("Ignoring foreground fallback to {WindowHandle} after window {ClosedWindowHandle} closed", windowHandle, transition.PreviousWindowHandle);
            return;
        }

        if (!isWindowHandleValid(windowHandle))
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

        Dispatch(() =>  {  if (requireStarted && !IsStarted())  {  return;  }   handler(this, windowHandle);  });
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
        catch (COMException exception)
        {
            logger.LogDebug(exception, "Dispatcher failed while publishing foreground window state");
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


    internal sealed class ForegroundTransitionHistory
    {
        private ForegroundTransition pendingTransition;
        private nint observedWindowHandle;
        private nint observedPreviousWindowHandle;
        private bool observedWindowDestroyed;

        public void RecordForeground(nint windowHandle)
        {
            pendingTransition = new(windowHandle, observedWindowHandle, observedPreviousWindowHandle, observedWindowDestroyed);
            observedPreviousWindowHandle = observedWindowHandle;
            observedWindowHandle = windowHandle;
            observedWindowDestroyed = false;
        }


        public void RecordDestroyed(nint windowHandle)
        {
            if (windowHandle == observedWindowHandle)
            {
                observedWindowDestroyed = true;
            }

            if (windowHandle == pendingTransition.PreviousWindowHandle)
            {
                pendingTransition = pendingTransition with
                {
                    PreviousWindowDestroyed = true
                };
            }
        }


        public ForegroundTransition TakePending()
        {
            ForegroundTransition transition = pendingTransition;
            pendingTransition = default;
            return transition;
        }


        public void Reset()
        {
            pendingTransition = default;
            observedWindowHandle = 0;
            observedPreviousWindowHandle = 0;
            observedWindowDestroyed = false;
        }
    }


    internal readonly record struct ForegroundTransition(nint WindowHandle, nint PreviousWindowHandle, nint PreviousPreviousWindowHandle, bool PreviousWindowDestroyed)
    {
        public bool IsCloseFallback => PreviousWindowDestroyed && PreviousWindowHandle != 0 && WindowHandle == PreviousPreviousWindowHandle;
    }
}
