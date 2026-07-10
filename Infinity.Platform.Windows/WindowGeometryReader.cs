using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public class WindowGeometryReader : 
    IWindowGeometryReader
{
    private const int OffscreenParkingCoordinate = -32000;

    public bool IsMinimised(nint windowHandle) =>
        PInvoke.IsIconic(new HWND(windowHandle));

    public bool IsVisible(nint windowHandle) =>
        PInvoke.IsWindowVisible(new HWND(windowHandle));

    public bool TryReadGeometry(nint windowHandle, out int x, out int y, out int width, out int height)
    {
        bool success = PInvoke.GetWindowRect(new HWND(windowHandle), out RECT rect);

        x = rect.left;
        y = rect.top;
        width = rect.right - rect.left;
        height = rect.bottom - rect.top;

        bool isOffscreenParked = rect.left == OffscreenParkingCoordinate &&
            rect.top == OffscreenParkingCoordinate;

        return success && !isOffscreenParked && width >= 10 && height >= 10;
    }
}
