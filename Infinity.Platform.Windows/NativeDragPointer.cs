using System.Drawing;
using Windows.Win32;

namespace Infinity.Platform.Windows;

public static class NativeDragPointer
{
    public static bool IsButtonDown => (PInvoke.GetAsyncKeyState(0x01) & 0x8000) != 0 || (PInvoke.GetAsyncKeyState(0x02) & 0x8000) != 0;

    public static bool TryGetPosition(out int x, out int y)
    {
        bool result = PInvoke.GetCursorPos(out Point point);
        x = point.X;
        y = point.Y;
        return result;
    }
}
