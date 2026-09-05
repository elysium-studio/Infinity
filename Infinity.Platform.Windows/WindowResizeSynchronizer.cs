using System.Drawing;
using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed unsafe class WindowResizeSynchronizer(ILogger<WindowResizeSynchronizer> logger) : IWindowResizeSynchronizer
{
    private const int CompositorFramesToWait = 2;
    private const uint CompositorClockTimeoutMilliseconds = 50;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;
    private const SET_WINDOW_POS_FLAGS ResizeFlags = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_NOCOPYBITS;
    private const REDRAW_WINDOW_FLAGS RedrawFlags = REDRAW_WINDOW_FLAGS.RDW_INVALIDATE | REDRAW_WINDOW_FLAGS.RDW_ERASE | REDRAW_WINDOW_FLAGS.RDW_FRAME | REDRAW_WINDOW_FLAGS.RDW_ALLCHILDREN | REDRAW_WINDOW_FLAGS.RDW_UPDATENOW;

    public bool TrySynchronize(nint windowHandle, int width, int height)
    {
        HWND hwnd = new(windowHandle);
        if (windowHandle == 0 || width <= 0 || height <= 0 || !PInvoke.IsWindow(hwnd) || !PInvoke.GetWindowRect(hwnd, out RECT windowBounds) || !TryGetActiveWorkArea(out RECT workArea))
        {
            return false;
        }

        int maximumX = Math.Max(workArea.left, workArea.right - width);
        int maximumY = Math.Max(workArea.top, workArea.bottom - height);
        int stagingX = Math.Clamp(windowBounds.left, workArea.left, maximumX);
        int stagingY = Math.Clamp(windowBounds.top, workArea.top, maximumY);
        if (!PInvoke.SetWindowPos(hwnd, HWND.Null, stagingX, stagingY, width, height, ResizeFlags))
        {
            logger.LogWarning("Could not stage window resize for DWM capture. Handle={Handle} Error={Error}", windowHandle, Marshal.GetLastPInvokeError());
            return false;
        }

        if (!PInvoke.RedrawWindow(hwnd, null, default, RedrawFlags))
        {
            logger.LogDebug("Could not synchronously redraw staged window. Handle={Handle} Error={Error}", windowHandle, Marshal.GetLastPInvokeError());
        }

        for (int frame = 0; frame < CompositorFramesToWait; frame++)
        {
            uint waitResult = PInvoke.DCompositionWaitForCompositorClock(0, null, CompositorClockTimeoutMilliseconds);
            if (waitResult == WaitObject0)
            {
                continue;
            }

            if (waitResult != WaitTimeout)
            {
                logger.LogDebug("Could not wait for staged window composition. Handle={Handle} Result=0x{Result:X8}", windowHandle, waitResult);
            }

            break;
        }

        return true;
    }


    private static bool TryGetActiveWorkArea(out RECT workArea)
    {
        PInvoke.GetCursorPos(out Point cursor);
        HMONITOR monitor = PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MONITORINFO monitorInfo = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };
        if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
        {
            workArea = default;
            return false;
        }

        workArea = monitorInfo.rcWork;
        return workArea.right > workArea.left && workArea.bottom > workArea.top;
    }
}
