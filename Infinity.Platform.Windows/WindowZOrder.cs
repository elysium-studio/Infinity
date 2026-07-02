using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowZOrder(IWindowStore repository,
    IWindowEventListener listener,
    IWindowFocusGuard focusGuard,
    Func<nint> handleFactory,
    IDispatcher dispatcher,
    ILogger<WindowZOrder> logger) :
    IWindowZOrder
{
    private const SET_WINDOW_POS_FLAGS SwpNoActivate = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
    private const SET_WINDOW_POS_FLAGS SwpNoMove = SET_WINDOW_POS_FLAGS.SWP_NOMOVE;
    private const SET_WINDOW_POS_FLAGS Swp_NOSIZE = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
    private const SET_WINDOW_POS_FLAGS SwpNoOwnerZOrder = SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;
    private const SET_WINDOW_POS_FLAGS SwpAsyncWindowPos = SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS;

    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(35);

    private static readonly HWND HwndTopmost = new(new nint(-1));
    private static readonly HWND HwndNotTopmost = new(new nint(-2));

    private readonly Lock refreshLock = new();
    private readonly Dictionary<nint, int> zOrderMap = new();

    private WNDENUMPROC? enumWindowsCallback;
    private HashSet<nint>? enumTrackedHandles;
    private int enumIndex;

    private int isStarted;
    private int refreshInFlight;
    private int refreshRequested;
    private nint pendingFocusHandle;

    public event EventHandler<nint>? FocusedWindowChanged;

    public event EventHandler? ZOrderChanged;

    public void Start()
    {
        if (Interlocked.CompareExchange(ref isStarted, 1, 0) != 0)
        {
            return;
        }

        listener.ForegroundChanged += HandleForegroundChanged;
        listener.MinimizeEnded += HandleMinimizeEnded;
        listener.ZOrderChanged += HandleZOrderChanged;

        ScheduleRefresh();
    }

    public void Stop()
    {
        if (Interlocked.CompareExchange(ref isStarted, 0, 1) != 1)
        {
            return;
        }

        listener.ForegroundChanged -= HandleForegroundChanged;
        listener.MinimizeEnded -= HandleMinimizeEnded;
        listener.ZOrderChanged -= HandleZOrderChanged;

        Volatile.Write(ref refreshRequested, 0);
        Interlocked.Exchange(ref pendingFocusHandle, 0);
    }

    public void BringToFront(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        if (!IsWindowHandleValid(windowHandle))
        {
            logger.LogWarning("BringToFront skipped because the target window handle is invalid: {WindowHandle}", windowHandle);
            ScheduleRefresh();
            return;
        }

        nint appHandle = GetAppHandle();
        bool hasValidAppHandle = appHandle != 0 && appHandle != windowHandle && IsWindowHandleValid(appHandle);

        if (appHandle != 0 && !hasValidAppHandle)
        {
            logger.LogDebug("App window handle was not valid during BringToFront: {AppHandle}", appHandle);
        }

        HWND hwnd = new(windowHandle);
        SET_WINDOW_POS_FLAGS flags = SwpNoMove | Swp_NOSIZE | SwpNoActivate | SwpNoOwnerZOrder | SwpAsyncWindowPos;
        bool wasTopmost = IsTopmost(hwnd);

        logger.LogDebug("Bringing window to front: {WindowHandle}", windowHandle);

        bool promoted = TrySetWindowPos(hwnd, HwndTopmost, flags, "promote target window");
        bool restored = wasTopmost || TrySetWindowPos(hwnd, HwndNotTopmost, flags, "restore target window");

        if (hasValidAppHandle)
        {
            TrySetWindowPos(new HWND(appHandle), HwndTopmost, flags, "promote app window");
        }

        if (!promoted || !restored)
        {
            logger.LogWarning("BringToFront completed with one or more SetWindowPos failures: {WindowHandle}", windowHandle);
        }

        ScheduleRefresh();
    }

    public void NotifyFocusChanged(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        RaiseFocusedWindowChanged(windowHandle, false);
    }

    public void Refresh()
    {
        TrackedWindow[] trackedWindows = SnapshotTrackedWindows();
        Dictionary<nint, int> zOrderSnapshot = BuildZOrderSnapshot(trackedWindows);

        Dispatch(() => ApplyZOrderSnapshot(trackedWindows, zOrderSnapshot, false));
    }

    private void HandleForegroundChanged(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        Interlocked.Exchange(ref pendingFocusHandle, windowHandle);
        ScheduleRefresh();
    }

    private void HandleMinimizeEnded(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        Interlocked.Exchange(ref pendingFocusHandle, windowHandle);
        ScheduleRefresh();
    }

    private void HandleZOrderChanged()
    {
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (!IsStarted())
        {
            return;
        }

        Volatile.Write(ref refreshRequested, 1);

        if (Interlocked.CompareExchange(ref refreshInFlight, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(RunRefreshLoopAsync);
    }

    private async Task RunRefreshLoopAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(RefreshDelay).ConfigureAwait(false);

                Volatile.Write(ref refreshRequested, 0);

                if (IsStarted())
                {
                    TrackedWindow[] trackedWindows = SnapshotTrackedWindows();
                    Dictionary<nint, int> zOrderSnapshot = BuildZOrderSnapshot(trackedWindows);

                    Dispatch(() => ApplyZOrderSnapshot(trackedWindows, zOrderSnapshot, true));
                    PublishPendingFocus();
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Exception in z-order refresh loop");
            }

            Interlocked.Exchange(ref refreshInFlight, 0);

            if (!IsStarted())
            {
                return;
            }

            if (Volatile.Read(ref refreshRequested) == 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref refreshInFlight, 1, 0) != 0)
            {
                return;
            }
        }
    }

    private TrackedWindow[] SnapshotTrackedWindows()
    {
        try
        {
            List<TrackedWindow> trackedWindows = [.. repository];

            return trackedWindows.ToArray();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogDebug(exception, "Could not snapshot tracked windows because the repository changed during enumeration");
            return Array.Empty<TrackedWindow>();
        }
    }

    private Dictionary<nint, int> BuildZOrderSnapshot(TrackedWindow[] trackedWindows)
    {
        HashSet<nint> trackedHandles = BuildTrackedHandleSet(trackedWindows);

        if (trackedHandles.Count == 0)
        {
            return new Dictionary<nint, int>();
        }

        lock (refreshLock)
        {
            enumWindowsCallback ??= EnumWindowsCallback;
            enumTrackedHandles = trackedHandles;
            enumIndex = 0;
            zOrderMap.Clear();

            try
            {
                PInvoke.EnumWindows(enumWindowsCallback, new LPARAM(0));
                return new Dictionary<nint, int>(zOrderMap);
            }
            finally
            {
                enumTrackedHandles = null;
                zOrderMap.Clear();
                enumIndex = 0;
            }
        }
    }

    private BOOL EnumWindowsCallback(HWND hwnd, LPARAM lParam)
    {
        HashSet<nint>? trackedHandles = enumTrackedHandles;

        if (trackedHandles is null)
        {
            return true;
        }

        nint windowHandle = (nint)hwnd;

        if (trackedHandles.Contains(windowHandle))
        {
            zOrderMap[windowHandle] = enumIndex;

            if (zOrderMap.Count == trackedHandles.Count)
            {
                return false;
            }
        }

        enumIndex++;
        return true;
    }

    private static HashSet<nint> BuildTrackedHandleSet(TrackedWindow[] trackedWindows)
    {
        HashSet<nint> trackedHandles = [];

        foreach (TrackedWindow trackedWindow in trackedWindows)
        {
            nint windowHandle = trackedWindow.Handle;

            if (windowHandle != 0)
            {
                trackedHandles.Add(windowHandle);
            }
        }

        return trackedHandles;
    }

    private void ApplyZOrderSnapshot(TrackedWindow[] trackedWindows, Dictionary<nint, int> zOrderSnapshot, bool requireStarted)
    {
        if (requireStarted && !IsStarted())
        {
            return;
        }

        bool changed = false;

        foreach (TrackedWindow trackedWindow in trackedWindows)
        {
            nint windowHandle = trackedWindow.Handle;
            int zIndex = windowHandle != 0 && zOrderSnapshot.TryGetValue(windowHandle, out int value) ? value : int.MaxValue;

            if (trackedWindow.ZIndex == zIndex)
            {
                continue;
            }

            trackedWindow.ZIndex = zIndex;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        ZOrderChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PublishPendingFocus()
    {
        nint windowHandle = Interlocked.Exchange(ref pendingFocusHandle, 0);

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

        RaiseFocusedWindowChanged(windowHandle, true);
    }

    private void RaiseFocusedWindowChanged(nint windowHandle, bool requireStarted)
    {
        EventHandler<nint>? handler = FocusedWindowChanged;

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
            logger.LogDebug(exception, "Focus guard failed while checking direct focus: {WindowHandle}", windowHandle);
            return false;
        }
    }

    private nint GetAppHandle()
    {
        try
        {
            return handleFactory();
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Could not get app window handle");
            return 0;
        }
    }

    private bool TrySetWindowPos(HWND hwnd, HWND insertAfter, SET_WINDOW_POS_FLAGS flags, string operation)
    {
        try
        {
            BOOL result = PInvoke.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, flags);

            if (result)
            {
                return true;
            }

            int error = Marshal.GetLastPInvokeError();

            logger.LogDebug("SetWindowPos failed while trying to {Operation}. Hwnd: {Hwnd}, InsertAfter: {InsertAfter}, Error: {Error}",
                operation,
                (nint)hwnd,
                (nint)insertAfter,
                error);

            return false;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "SetWindowPos threw while trying to {Operation}. Hwnd: {Hwnd}, InsertAfter: {InsertAfter}",
                operation,
                (nint)hwnd,
                (nint)insertAfter);

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
            logger.LogDebug(exception, "Dispatcher was disposed while publishing z-order state");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogDebug(exception, "Dispatcher rejected z-order state publication");
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

    private static bool IsTopmost(HWND hwnd)
    {
        nint extendedStyle = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return (extendedStyle & (long)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;
    }
}