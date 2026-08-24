using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed class WindowInputTransparencyController
{
    public void SetInputEnabled(nint windowHandle, bool enabled)
    {
        if (windowHandle == 0)
        {
            return;
        }

        HWND handle = new(windowHandle);
        WINDOW_EX_STYLE style = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(handle,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        WINDOW_EX_STYLE updated = enabled
            ? style & ~WINDOW_EX_STYLE.WS_EX_TRANSPARENT
            : style | WINDOW_EX_STYLE.WS_EX_TRANSPARENT;

        if (updated != style)
        {
            _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)updated);
        }
    }
}