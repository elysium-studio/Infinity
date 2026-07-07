using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowConcealer :
    IWindowConcealer,
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

    private static readonly nint HwndTop = 0;

    private const SET_WINDOW_POS_FLAGS MoveFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
        SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
        SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
        SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING |
        SET_WINDOW_POS_FLAGS.SWP_NOCOPYBITS;

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
        }

        Move(windowHandle, OffscreenParkX, OffscreenParkY);

        return true;
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
            concealedWindows.Remove(windowHandle);
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
            isDisposed = true;
            strandedWindows = [.. concealedWindows];
            concealedWindows.Clear();
        }

        foreach ((nint windowHandle, WindowOrigin origin) in strandedWindows)
        {
            Move(windowHandle, origin.X, origin.Y);
        }
    }

    private static void Move(nint windowHandle, int x, int y)
    {
        HWND hwnd = new(windowHandle);

        if (!PInvoke.IsWindow(hwnd))
        {
            return;
        }

        PInvoke.SetWindowPos(hwnd, new HWND(HwndTop), x, y, 0, 0, MoveFlags);
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