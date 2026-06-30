using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public class WindowEnumerator : 
    IWindowEnumerator
{
    public void EnumerateVisible(Action<nint> action)
    {
        PInvoke.EnumWindows((windowHandle, _) =>
        {
            action(windowHandle);
            return true;
        }, new LPARAM(0));
    }
}