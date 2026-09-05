using System.Drawing;
using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed unsafe partial class WindowBoundaryResizer : IWindowBoundaryResizer
{
    private const uint GetMinMaxInfo = 0x0024;
    private const uint AbortIfHungAndBlock = 0x0003;
    private const uint QueryTimeoutMilliseconds = 20;
    private const SET_WINDOW_POS_FLAGS ResizeFlags = SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;

    public bool TryGetLimits(nint handle, out WindowResizeLimits limits)
    {
        limits = default;
        HWND hwnd = new(handle);
        if (!CanResize(hwnd))
        {
            return false;
        }

        uint dpi = GetDpiForWindow(handle);
        if (dpi == 0)
        {
            return false;
        }

        MinMaxInfo info = new()
        {
            MinimumTrackSize = new(GetSystemMetricsForDpi(34, dpi), GetSystemMetricsForDpi(35, dpi)),
            MaximumTrackSize = new(GetSystemMetricsForDpi(59, dpi), GetSystemMetricsForDpi(60, dpi))
        };
        if (SendMessageTimeoutW(handle, GetMinMaxInfo, 0, &info, AbortIfHungAndBlock, QueryTimeoutMilliseconds, out _) == 0)
        {
            return false;
        }

        limits = new(Math.Max(1, info.MinimumTrackSize.X), Math.Max(1, info.MinimumTrackSize.Y), info.MaximumTrackSize.X, info.MaximumTrackSize.Y);
        return limits.MaximumWidth >= limits.MinimumWidth && limits.MaximumHeight >= limits.MinimumHeight;
    }

    public bool TryResize(nint handle, int x, int y, int width, int height)
    {
        HWND hwnd = new(handle);
        return width > 0 && height > 0 && CanResize(hwnd) && PInvoke.SetWindowPos(hwnd, HWND.Null, x, y, width, height, ResizeFlags);
    }

    private static bool CanResize(HWND hwnd) => PInvoke.IsWindow(hwnd) && PInvoke.IsWindowVisible(hwnd) && !PInvoke.IsIconic(hwnd) && !PInvoke.IsZoomed(hwnd) && !PInvoke.IsHungAppWindow(hwnd) && ((WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE) & WINDOW_STYLE.WS_THICKFRAME) != 0;

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport("user32.dll")]
    private static partial nint SendMessageTimeoutW(nint hwnd, uint message, nuint wParam, MinMaxInfo* info, uint flags, uint timeout, out nuint result);

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaximumSize;
        public Point MaximumPosition;
        public Point MinimumTrackSize;
        public Point MaximumTrackSize;
    }
}
