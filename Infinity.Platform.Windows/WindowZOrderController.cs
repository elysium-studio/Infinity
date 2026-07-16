using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowZOrderController(ILogger<WindowZOrderController> logger) :
    IWindowZOrderController
{
    private const SET_WINDOW_POS_FLAGS PositionFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
        SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
        SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
        SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS;

    private static readonly HWND HwndTop = new(0);
    private static readonly HWND HwndTopmost = new(new nint(-1));

    public IDisposable? ElevateTemporarily(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);

        if (windowHandle == 0 || !PInvoke.IsWindow(hwnd))
        {
            return null;
        }

        _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint processId);
        bool isTopmost = IsTopmost(hwnd);
        HWND previous = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_HWNDPREV);

        if (!previous.IsNull && IsTopmost(previous) != isTopmost)
        {
            previous = default;
        }

        HWND restoreAfter = previous.IsNull
            ? isTopmost ? HwndTopmost : HwndTop
            : previous;
        HWND elevateAfter = isTopmost ? HwndTopmost : HwndTop;

        if (!TrySetWindowPos(hwnd, elevateAfter, "elevate window for thumbnail drag"))
        {
            return null;
        }

        return new ZOrderLease(this, windowHandle, processId, isTopmost, restoreAfter);
    }

    private void Restore(nint windowHandle, uint processId, bool wasTopmost, HWND restoreAfter)
    {
        HWND hwnd = new(windowHandle);

        if (!PInvoke.IsWindow(hwnd))
        {
            return;
        }

        _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint currentProcessId);

        if (currentProcessId != processId)
        {
            return;
        }

        if (!restoreAfter.IsNull &&
            (!PInvoke.IsWindow(restoreAfter) || IsTopmost(restoreAfter) != wasTopmost))
        {
            restoreAfter = wasTopmost ? HwndTopmost : HwndTop;
        }

        _ = TrySetWindowPos(hwnd, restoreAfter, "restore window after thumbnail drag");
    }

    private bool TrySetWindowPos(HWND hwnd, HWND insertAfter, string operation)
    {
        try
        {
            if (PInvoke.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, PositionFlags))
            {
                return true;
            }

            logger.LogDebug("SetWindowPos failed while trying to {Operation}. Hwnd: {Hwnd}, InsertAfter: {InsertAfter}, Error: {Error}",
                operation,
                (nint)hwnd,
                (nint)insertAfter,
                Marshal.GetLastPInvokeError());
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception,
                "SetWindowPos threw while trying to {Operation}. Hwnd: {Hwnd}, InsertAfter: {InsertAfter}",
                operation,
                (nint)hwnd,
                (nint)insertAfter);
        }

        return false;
    }

    private static bool IsTopmost(HWND hwnd)
    {
        nint extendedStyle = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        return (extendedStyle & (long)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;
    }

    private sealed class ZOrderLease(WindowZOrderController owner,
        nint windowHandle,
        uint processId,
        bool wasTopmost,
        HWND restoreAfter) :
        IDisposable
    {
        private WindowZOrderController? owner = owner;

        public void Dispose()
        {
            WindowZOrderController? currentOwner = Interlocked.Exchange(ref owner, null);

            if (currentOwner is null)
            {
                return;
            }

            currentOwner.Restore(windowHandle, processId, wasTopmost, restoreAfter);
            GC.SuppressFinalize(this);
        }
    }
}
