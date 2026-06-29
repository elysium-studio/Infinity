using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Infinity.Platform.Windows;

public class WindowEnumerator : 
    IWindowEnumerator
{
    public void EnumerateVisible(Action<nint> onWindowFound)
    {
        PInvoke.EnumWindows((windowHandle, _) =>
        {
            onWindowFound(windowHandle);
            return true;
        }, new LPARAM(0));
    }
}