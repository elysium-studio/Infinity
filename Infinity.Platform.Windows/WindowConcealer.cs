using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed unsafe class WindowConcealer(IWindowEnumerator enumerator,
    ILogger<WindowConcealer> logger) :
    IWindowConcealer,
    IWindowConcealmentRecovery,
    IDisposable
{
    private readonly Dictionary<nint, WindowOrigin> concealedWindows = [];
    private readonly Lock syncRoot = new();

    private bool isDisposed;

    private const int WsChild = 0x40000000;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private const int OffscreenParkX = -32000;
    private const int OffscreenParkY = -32000;

    private const string RecoveryMarkerProperty = "Elysium.Infinity.WindowConcealer.Recovery";
    private const string RecoveryXProperty = "Elysium.Infinity.WindowConcealer.OriginX";
    private const string RecoveryYProperty = "Elysium.Infinity.WindowConcealer.OriginY";

    private static readonly nint HwndTop = 0;

    private const SET_WINDOW_POS_FLAGS MoveFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
        SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
        SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
        SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING |
        SET_WINDOW_POS_FLAGS.SWP_NOCOPYBITS;

    public void RecoverStrandedWindows()
    {
        enumerator.EnumerateVisible(windowHandle =>
        {
            HWND hwnd = new(windowHandle);

            lock (syncRoot)
            {
                if (isDisposed || concealedWindows.ContainsKey(windowHandle))
                {
                    return;
                }

                if (!TryReadRecoveryOrigin(hwnd, out WindowOrigin origin))
                {
                    return;
                }

                if (!Move(windowHandle, origin.X, origin.Y))
                {
                    logger.LogWarning("Could not recover a concealed window. Handle={WindowHandle}, Error={Error}",
                        windowHandle,
                        Marshal.GetLastPInvokeError());
                    return;
                }

                ClearRecoveryOrigin(hwnd);
                logger.LogInformation("Recovered a window concealed by a previous session. Handle={WindowHandle}, X={WindowX}, Y={WindowY}",
                    windowHandle,
                    origin.X,
                    origin.Y);
            }
        });
    }

    public bool Conceal(nint windowHandle)
    {
        windowHandle = GetTargetWindowHandle(windowHandle);

        if (windowHandle == default)
        {
            return false;
        }

        HWND hwnd = new(windowHandle);

        if (!ShouldControlWindow(hwnd))
        {
            return false;
        }

        if (!TryGetWindowOrigin(hwnd, out WindowOrigin origin))
        {
            return false;
        }

        lock (syncRoot)
        {
            if (isDisposed)
            {
                return false;
            }

            if (!concealedWindows.TryAdd(windowHandle, origin))
            {
                return true;
            }

            if (!TryWriteRecoveryOrigin(hwnd, origin))
            {
                concealedWindows.Remove(windowHandle);
                return false;
            }

            if (!Move(windowHandle, OffscreenParkX, OffscreenParkY))
            {
                concealedWindows.Remove(windowHandle);
                ClearRecoveryOrigin(hwnd);
                logger.LogWarning("Could not conceal a window. Handle={WindowHandle}, Error={Error}",
                    windowHandle,
                    Marshal.GetLastPInvokeError());
                return false;
            }

            return true;
        }
    }

    public void Reveal(nint windowHandle)
    {
        windowHandle = GetTargetWindowHandle(windowHandle);

        if (windowHandle == default)
        {
            return;
        }

        lock (syncRoot)
        {
            if (concealedWindows.Remove(windowHandle))
            {
                ClearRecoveryOrigin(new HWND(windowHandle));
            }
        }
    }

    public bool IsConcealed(nint windowHandle)
    {
        windowHandle = GetTargetWindowHandle(windowHandle);

        if (windowHandle == default)
        {
            return false;
        }

        lock (syncRoot)
        {
            return concealedWindows.ContainsKey(windowHandle);
        }
    }

    public IReadOnlySet<nint> ConcealedHandles()
    {
        lock (syncRoot)
        {
            return new HashSet<nint>(concealedWindows.Keys);
        }
    }

    public void Dispose()
    {
        List<KeyValuePair<nint, WindowOrigin>> strandedWindows;

        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            strandedWindows = [.. concealedWindows];
            concealedWindows.Clear();
        }

        foreach ((nint windowHandle, WindowOrigin origin) in strandedWindows)
        {
            HWND hwnd = new(windowHandle);

            if (Move(windowHandle, origin.X, origin.Y))
            {
                ClearRecoveryOrigin(hwnd);
            }
            else
            {
                logger.LogWarning("Could not restore a concealed window during shutdown. Handle={WindowHandle}, Error={Error}",
                    windowHandle,
                    Marshal.GetLastPInvokeError());
            }
        }

        GC.SuppressFinalize(this);
    }

    private bool TryWriteRecoveryOrigin(HWND hwnd, WindowOrigin origin)
    {
        if (!SetWindowProperty(hwnd, RecoveryXProperty, new HANDLE(origin.X)) ||
            !SetWindowProperty(hwnd, RecoveryYProperty, new HANDLE(origin.Y)) ||
            !SetWindowProperty(hwnd, RecoveryMarkerProperty, new HANDLE(1)))
        {
            int error = Marshal.GetLastPInvokeError();
            ClearRecoveryOrigin(hwnd);
            logger.LogWarning("Could not record concealed-window recovery data. Handle={WindowHandle}, Error={Error}",
                (nint)hwnd,
                error);
            return false;
        }

        return true;
    }

    private static bool TryReadRecoveryOrigin(HWND hwnd, out WindowOrigin origin)
    {
        if (GetWindowProperty(hwnd, RecoveryMarkerProperty) == default)
        {
            origin = default;
            return false;
        }

        int x = checked((int)(nint)GetWindowProperty(hwnd, RecoveryXProperty).Value);
        int y = checked((int)(nint)GetWindowProperty(hwnd, RecoveryYProperty).Value);
        origin = new WindowOrigin(x, y);
        return true;
    }

    private static bool SetWindowProperty(HWND hwnd, string name, HANDLE value)
    {
        fixed (char* namePointer = name)
        {
            return PInvoke.SetProp(hwnd, namePointer, value);
        }
    }

    private static HANDLE GetWindowProperty(HWND hwnd, string name)
    {
        fixed (char* namePointer = name)
        {
            return PInvoke.GetProp(hwnd, namePointer);
        }
    }

    private static void ClearRecoveryOrigin(HWND hwnd)
    {
        RemoveWindowProperty(hwnd, RecoveryMarkerProperty);
        RemoveWindowProperty(hwnd, RecoveryXProperty);
        RemoveWindowProperty(hwnd, RecoveryYProperty);
    }

    private static void RemoveWindowProperty(HWND hwnd, string name)
    {
        fixed (char* namePointer = name)
        {
            PInvoke.RemoveProp(hwnd, namePointer);
        }
    }

    private static bool Move(nint windowHandle, int x, int y)
    {
        HWND hwnd = new(windowHandle);

        if (!PInvoke.IsWindow(hwnd))
        {
            return true;
        }

        return PInvoke.SetWindowPos(hwnd, new HWND(HwndTop), x, y, 0, 0, MoveFlags);
    }

    private static bool TryGetWindowOrigin(HWND hwnd, out WindowOrigin origin)
    {
        origin = default;

        if (!PInvoke.GetWindowRect(hwnd, out RECT rectangle))
        {
            return false;
        }

        origin = new WindowOrigin(rectangle.left, rectangle.top);

        return true;
    }

    private static bool ShouldControlWindow(HWND hwnd)
    {
        if (!PInvoke.IsWindow(hwnd) || !PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd))
        {
            return false;
        }

        int style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

        if ((style & WsChild) != 0)
        {
            return false;
        }

        int extendedStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if ((extendedStyle & WsExToolWindow) != 0)
        {
            return false;
        }

        if (IsOverlayLikeWindow(hwnd))
        {
            return false;
        }

        return true;
    }

    private static bool IsOverlayLikeWindow(HWND hwnd)
    {
        if (!PInvoke.IsWindow(hwnd))
        {
            return false;
        }

        int extendedStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        bool isLayered = (extendedStyle & WsExLayered) != 0;
        bool isTransparent = (extendedStyle & WsExTransparent) != 0;
        bool isNoActivate = (extendedStyle & WsExNoActivate) != 0;
        bool isToolWindow = (extendedStyle & WsExToolWindow) != 0;

        return isLayered && isTransparent && isNoActivate && isToolWindow;
    }

    private static nint GetTargetWindowHandle(nint windowHandle)
    {
        if (windowHandle == default)
        {
            return default;
        }

        HWND hwnd = new(windowHandle);

        if (!PInvoke.IsWindow(hwnd))
        {
            return default;
        }

        HWND rootHwnd = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);

        if (rootHwnd == default)
        {
            return windowHandle;
        }

        return rootHwnd;
    }

    private readonly record struct WindowOrigin(int X, int Y);
}