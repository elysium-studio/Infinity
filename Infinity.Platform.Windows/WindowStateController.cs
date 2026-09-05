using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed class WindowStateController : IWindowStateController
{
    public WindowCommandState GetState(nint windowHandle)
    {
        HWND window = new(windowHandle);
        if (windowHandle == 0 || !PInvoke.IsWindow(window))
        {
            return WindowCommandState.Unavailable;
        }

        WINDOW_STYLE style = (WINDOW_STYLE)(uint)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        bool isMinimized = PInvoke.IsIconic(window);
        bool isMaximized = PInvoke.IsZoomed(window);
        bool hasMinimizeBox = style.HasFlag(WINDOW_STYLE.WS_MINIMIZEBOX);
        bool hasMaximizeBox = style.HasFlag(WINDOW_STYLE.WS_MAXIMIZEBOX);
        return new(hasMinimizeBox && !isMinimized, hasMaximizeBox && !isMaximized, isMaximized);
    }


    public bool TryMaximize(nint windowHandle) => TryShow(windowHandle, SHOW_WINDOW_CMD.SW_MAXIMIZE);

    public bool TryRestore(nint windowHandle) => TryShow(windowHandle, SHOW_WINDOW_CMD.SW_RESTORE);

    public bool TryRestoreForMove(nint windowHandle, out WindowRestoreBounds bounds)
    {
        bounds = default;
        HWND window = new(windowHandle);
        if (windowHandle == 0 || !PInvoke.IsWindow(window) || PInvoke.IsHungAppWindow(window))
        {
            return false;
        }

        if (PInvoke.IsZoomed(window))
        {
            _ = PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        if (PInvoke.IsZoomed(window) || !PInvoke.GetWindowRect(window, out RECT rectangle) || rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return false;
        }

        bounds = new(rectangle.left, rectangle.top, rectangle.Width, rectangle.Height);
        return true;
    }


    public bool TryMinimize(nint windowHandle) => TryShow(windowHandle, SHOW_WINDOW_CMD.SW_MINIMIZE);

    private static bool TryShow(nint windowHandle, SHOW_WINDOW_CMD command)
    {
        HWND window = new(windowHandle);
        if (windowHandle == 0 || !PInvoke.IsWindow(window))
        {
            return false;
        }

        _ = PInvoke.ShowWindowAsync(window, command);
        return true;
    }
}
