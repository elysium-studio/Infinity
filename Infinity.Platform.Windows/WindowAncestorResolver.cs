using Infinity.Platform.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowAncestorResolver : 
    IWindowAncestorResolver
{
    public nint GetRootAncestor(nint windowHandle) =>
        PInvoke.GetAncestor(new HWND(windowHandle), GET_ANCESTOR_FLAGS.GA_ROOT);
}
