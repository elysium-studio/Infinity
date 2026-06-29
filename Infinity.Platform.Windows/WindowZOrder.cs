using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowZOrder(IWindowStore repository,
    IWindowEventListener listener,
    IWindowFocusGuard focusGuard,
    Func<nint> handleFactory,
    ILogger<WindowZOrder> logger) :
    IWindowZOrder
{
    private const SET_WINDOW_POS_FLAGS SwpNoActivate = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
    private const SET_WINDOW_POS_FLAGS SwpNoMove = SET_WINDOW_POS_FLAGS.SWP_NOMOVE;
    private const SET_WINDOW_POS_FLAGS SwpNoSize = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
    private const SET_WINDOW_POS_FLAGS SwpNoOwnerZOrder = SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;

    private static readonly HWND HwndTopmost = new(new nint(-1));
    private static readonly HWND HwndNotTopmost = new(new nint(-2));

    private readonly Lock refreshLock = new();
    private readonly Dictionary<nint, int> zOrderMap = new();
    private WNDENUMPROC? enumWindowsCallback;
    private int enumIndex;

    public event EventHandler<nint>? FocusedWindowChanged;
    public event EventHandler? ZOrderChanged;

    private int refreshInFlight;
    private int refreshRequested;
    private nint pendingFocusHandle;
    private bool disposed;

    public void Start()
    {
        listener.ForegroundChanged += HandleForegroundChanged;
        listener.MinimizeEnded += HandleMinimizeEnded;
        listener.ZOrderChanged += HandleZOrderChanged;
    }

    public void Stop()
    {
        disposed = true;
        listener.ForegroundChanged -= HandleForegroundChanged;
        listener.MinimizeEnded -= HandleMinimizeEnded;
        listener.ZOrderChanged -= HandleZOrderChanged;
    }

    public void BringToFront(nint windowHandle)
    {
        nint appHandle = handleFactory();

        if (!CanMoveWindow(windowHandle, appHandle))
        {
            logger.LogWarning("BringToFront skipped — invalid window or app handle ({WindowHandle}, {AppHandle})", windowHandle, appHandle);
            return;
        }

        logger.LogInformation("Bringing window to front: {WindowHandle}", windowHandle);

        HWND hwnd = new(windowHandle);
        HWND appHwnd = new(appHandle);
        SET_WINDOW_POS_FLAGS flags = SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder;

        PInvoke.SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, flags);
        PInvoke.SetWindowPos(hwnd, HwndNotTopmost, 0, 0, 0, 0, flags);
        PInvoke.SetWindowPos(appHwnd, HwndTopmost, 0, 0, 0, 0, flags);

        Refresh();
    }

    public void NotifyFocusChanged(nint windowHandle) =>
        FocusedWindowChanged?.Invoke(this, windowHandle);

    public void Refresh()
    {
        lock (refreshLock)
        {
            enumWindowsCallback ??= EnumWindowsCallback;

            zOrderMap.Clear();
            enumIndex = 0;

            PInvoke.EnumWindows(enumWindowsCallback, new LPARAM(0));

            foreach (TrackedWindow trackedWindow in repository)
            {
                trackedWindow.ZIndex = zOrderMap.TryGetValue(trackedWindow.Handle, out int zIndex) ? zIndex : int.MaxValue;
            }
        }

        ZOrderChanged?.Invoke(this, EventArgs.Empty);
    }

    private BOOL EnumWindowsCallback(HWND hwnd, LPARAM lParam)
    {
        zOrderMap[(nint)hwnd] = enumIndex++;
        return true;
    }

    private void HandleForegroundChanged(nint windowHandle)
    {
        Interlocked.Exchange(ref pendingFocusHandle, windowHandle);
        ScheduleRefresh();
    }

    private void HandleMinimizeEnded(nint windowHandle)
    {
        Interlocked.Exchange(ref pendingFocusHandle, windowHandle);
        ScheduleRefresh();
    }

    private void HandleZOrderChanged() => ScheduleRefresh();

    private void ScheduleRefresh()
    {
        Volatile.Write(ref refreshRequested, 1);

        if (Interlocked.CompareExchange(ref refreshInFlight, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(RunRefreshLoop);
    }

    private void RunRefreshLoop()
    {
        while (true)
        {
            Volatile.Write(ref refreshRequested, 0);

            if (disposed)
            {
                Interlocked.Exchange(ref refreshInFlight, 0);
                return;
            }

            try
            {
                Refresh();

                nint focusHandle = Interlocked.Exchange(ref pendingFocusHandle, 0);

                if (focusHandle != 0 && !focusGuard.IsDirect(focusHandle))
                {
                    NotifyFocusChanged(focusHandle);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Exception in z-order refresh");
            }

            Interlocked.Exchange(ref refreshInFlight, 0);

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

    private static bool CanMoveWindow(nint windowHandle, nint appHandle)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        if (appHandle == 0)
        {
            return false;
        }

        if (windowHandle == appHandle)
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(windowHandle)))
        {
            return false;
        }

        if (!PInvoke.IsWindow(new HWND(appHandle)))
        {
            return false;
        }

        return true;
    }
}