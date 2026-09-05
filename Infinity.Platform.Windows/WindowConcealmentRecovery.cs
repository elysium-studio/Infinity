using System.Runtime.InteropServices;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed unsafe class WindowConcealmentRecovery(IWindowEnumerator enumerator, ILogger<WindowConcealmentRecovery> logger) : IWindowConcealmentRecovery
{
    private const string RecoveryMarkerProperty = "Elysium.Infinity.WindowConcealer.Recovery";
    private const string RecoveryXProperty = "Elysium.Infinity.WindowConcealer.OriginX";
    private const string RecoveryYProperty = "Elysium.Infinity.WindowConcealer.OriginY";
    private const SET_WINDOW_POS_FLAGS MoveFlags = SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING | SET_WINDOW_POS_FLAGS.SWP_NOCOPYBITS;

    public void RecoverStrandedWindows() => enumerator.EnumerateVisible(RecoverWindow);

    private void RecoverWindow(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);
        if (GetWindowProperty(hwnd, RecoveryMarkerProperty) == default)
        {
            return;
        }

        int x = checked((int)(nint)GetWindowProperty(hwnd, RecoveryXProperty).Value);
        int y = checked((int)(nint)GetWindowProperty(hwnd, RecoveryYProperty).Value);
        if (PInvoke.IsWindow(hwnd) && !PInvoke.SetWindowPos(hwnd, default, x, y, 0, 0, MoveFlags))
        {
            logger.LogWarning("Could not recover a concealed window. Handle={WindowHandle}, Error={Error}", windowHandle, Marshal.GetLastPInvokeError());
            return;
        }

        RemoveWindowProperty(hwnd, RecoveryMarkerProperty);
        RemoveWindowProperty(hwnd, RecoveryXProperty);
        RemoveWindowProperty(hwnd, RecoveryYProperty);
        logger.LogInformation("Recovered a window concealed by a previous session. Handle={WindowHandle}, X={WindowX}, Y={WindowY}", windowHandle, x, y);
    }

    private static HANDLE GetWindowProperty(HWND hwnd, string name)
    {
        fixed (char* namePointer = name)
        {
            return PInvoke.GetProp(hwnd, namePointer);
        }
    }

    private static void RemoveWindowProperty(HWND hwnd, string name)
    {
        fixed (char* namePointer = name)
        {
            PInvoke.RemoveProp(hwnd, namePointer);
        }
    }
}
