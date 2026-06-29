using Infinity.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowFilter(WindowFilterOptions options) : 
    IWindowFilter
{
    private const DWMWINDOWATTRIBUTE DwmwaCloaked = DWMWINDOWATTRIBUTE.DWMWA_CLOAKED;

    public unsafe bool ShouldTrack(nint windowHandle, nint ownerHandle)
    {
        HWND hwnd = new(windowHandle);

        if (windowHandle == ownerHandle)
        {
            return false;
        }

        if (hwnd == PInvoke.GetShellWindow())
        {
            return false;
        }

        if (hwnd == PInvoke.GetDesktopWindow())
        {
            return false;
        }

        if (PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) != hwnd)
        {
            return false;
        }

        int cloaked = 0;
        PInvoke.DwmGetWindowAttribute(hwnd, DwmwaCloaked, &cloaked, (uint)sizeof(int));

        if (cloaked != 0)
        {
            return false;
        }

        if (!PInvoke.GetWindowRect(hwnd, out RECT rect))
        {
            return false;
        }

        if (rect.left == 0 && rect.top == 0 && rect.right == 0 && rect.bottom == 0)
        {
            return false;
        }

        if (PInvoke.IsHungAppWindow(hwnd))
        {
            return false;
        }

        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        bool isToolWindow = (exStyle & 0x00000080) != 0;
        bool isAppWindow = (exStyle & 0x00040000) != 0;

        if (isToolWindow && !isAppWindow)
        {
            return false;
        }

        Span<char> classBuffer = stackalloc char[256];
        fixed (char* classPtr = classBuffer)
        {
            _ = PInvoke.GetClassName(hwnd, classPtr, 256);
        }

        if (options.BlockedClassNames.Contains(new string(classBuffer).TrimEnd('\0')))
        {
            return false;
        }

        Span<char> titleBuffer = stackalloc char[256];
        fixed (char* titlePtr = titleBuffer)
        {
            _ = PInvoke.GetWindowText(hwnd, titlePtr, 256);
        }

        if (string.IsNullOrWhiteSpace(new string(titleBuffer).TrimEnd('\0')))
        {
            return false;
        }

        PInvoke.GetWindowThreadProcessId(hwnd, out uint processId);

        if (processId == (uint)Environment.ProcessId)
        {
            return false;
        }

        if (IsElevated(processId))
        {
            return false;
        }

        try
        {
            Process process = Process.GetProcessById((int)processId);

            if (options.BlockedProcessNames.Contains(process.ProcessName))
            {
                return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }

        return true;
    }

    private static unsafe bool IsElevated(uint processId)
    {
        using SafeHandle processHandle = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);

        if (processHandle.IsInvalid)
        {
            return true;
        }

        if (!PInvoke.OpenProcessToken(processHandle, TOKEN_ACCESS_MASK.TOKEN_QUERY, out SafeFileHandle tokenHandle))
        {
            return true;
        }

        using (tokenHandle)
        {
            int elevated = 0;
            uint returnLength = 0;

            if (!PInvoke.GetTokenInformation(new HANDLE(tokenHandle.DangerousGetHandle()), TOKEN_INFORMATION_CLASS.TokenElevation, &elevated, (uint)sizeof(int), &returnLength))
            {
                return true;
            }

            return elevated != 0;
        }
    }
}