using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public sealed class WindowCloser : IWindowCloser
{
    private const uint WmClose = 0x0010;

    public bool TryClose(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);
        return windowHandle != 0 && PInvoke.IsWindow(hwnd) && PInvoke.PostMessage(hwnd, WmClose, default, default);
    }
}
