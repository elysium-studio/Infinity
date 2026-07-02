using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public class WindowTitleReader : 
    IWindowTitleReader
{
    public unsafe string GetTitle(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);

        Span<char> buffer = stackalloc char[256];
        fixed (char* bufferPtr = buffer)
        {
            int length = PInvoke.GetWindowText(hwnd, bufferPtr, 256);

            if (length > 0)
            {
                return new string(buffer[..length]);
            }
        }

        string childTitle = string.Empty;

        PInvoke.EnumChildWindows(hwnd, (child, _) =>
        {
            Span<char> childBuffer = stackalloc char[256];
            fixed (char* childBufferPtr = childBuffer)
            {
                int length = PInvoke.GetWindowText(child, childBufferPtr, 256);

                if (length > 0)
                {
                    childTitle = new string(childBuffer[..length]);
                    return false;
                }
            }

            return true;
        }, new LPARAM(0));

        return childTitle;
    }
}