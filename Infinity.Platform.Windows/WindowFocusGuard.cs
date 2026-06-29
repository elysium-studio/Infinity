using Infinity.Platform.Abstractions;
using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Elysium.Platform.Windows;

public class WindowFocusGuard : 
    IWindowFocusGuard
{
    public bool IsDirect(nint windowHandle)
    {
        PInvoke.GetCursorPos(out Point cursorPos);
        HWND windowUnderCursor = PInvoke.WindowFromPoint(cursorPos);

        while (windowUnderCursor != HWND.Null)
        {
            if (windowUnderCursor == new HWND(windowHandle))
            {
                return true;
            }

            windowUnderCursor = PInvoke.GetParent(windowUnderCursor);
        }

        return false;
    }
}