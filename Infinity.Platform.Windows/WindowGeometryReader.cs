using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Infinity.Platform.Windows;

public sealed class WindowGeometryReader : IWindowGeometryReader
{
    private const DWMWINDOWATTRIBUTE ExtendedFrameBounds = DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS;

    public bool IsMinimised(nint windowHandle) => PInvoke.IsIconic(new HWND(windowHandle));

    public bool IsVisible(nint windowHandle) => PInvoke.IsWindowVisible(new HWND(windowHandle));

    public bool TryReadGeometry(nint windowHandle, out int x, out int y, out int width, out int height)
    {
        bool success = PInvoke.GetWindowRect(new HWND(windowHandle), out RECT rect);
        SetGeometry(rect, out x, out y, out width, out height);
        return success && width >= 10 && height >= 10;
    }


    public unsafe bool TryReadVisibleGeometry(nint windowHandle, out int x, out int y, out int width, out int height)
    {
        RECT rect = default;
        HRESULT result = PInvoke.DwmGetWindowAttribute(new HWND(windowHandle), ExtendedFrameBounds, &rect, (uint)sizeof(RECT));
        SetGeometry(rect, out x, out y, out width, out height);
        return result.Succeeded && width >= 10 && height >= 10;
    }


    private static void SetGeometry(RECT rect, out int x, out int y, out int width, out int height)
    {
        x = rect.left;
        y = rect.top;
        width = rect.right - rect.left;
        height = rect.bottom - rect.top;
    }
}
