using Infinity.Platform.Abstractions;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Infinity.Platform.Windows;

public sealed class WindowApplicationIdentityProvider :
    IWindowApplicationIdentityProvider
{
    private const string ApplicationFrameHost = "ApplicationFrameHost.exe";

    public bool TryGetApplicationId(IntPtr windowHandle, out string applicationId)
    {
        PInvoke.GetWindowThreadProcessId(new HWND(windowHandle), out uint processId);

        if (processId == 0)
        {
            applicationId = string.Empty;
            return false;
        }

        bool hasExecutablePath = TryGetExecutablePath(processId, out string executablePath);

        if (!hasExecutablePath ||
            !string.Equals(Path.GetFileName(executablePath), ApplicationFrameHost, StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetApplicationUserModelId(processId, out applicationId))
            {
                return true;
            }

            if (hasExecutablePath)
            {
                applicationId = NormalizePath(executablePath);
                return true;
            }
        }

        string? childApplicationId = null;

        PInvoke.EnumChildWindows(new HWND(windowHandle), (childHandle, _) =>
        {
            PInvoke.GetWindowThreadProcessId(childHandle, out uint childProcessId);

            if (childProcessId == 0 || childProcessId == processId)
            {
                return true;
            }

            if (TryGetApplicationUserModelId(childProcessId, out childApplicationId))
            {
                return false;
            }

            if (!TryGetExecutablePath(childProcessId, out string childExecutablePath))
            {
                return true;
            }

            childApplicationId = NormalizePath(childExecutablePath);
            return false;
        }, new LPARAM(0));

        if (childApplicationId is not null)
        {
            applicationId = childApplicationId;
            return true;
        }

        if (TryGetApplicationUserModelId(processId, out applicationId))
        {
            return true;
        }

        if (hasExecutablePath)
        {
            applicationId = NormalizePath(executablePath);
            return true;
        }

        applicationId = string.Empty;
        return false;
    }

    private static bool TryGetApplicationUserModelId(uint processId, out string applicationId)
    {
        using SafeHandle processHandle = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            processId);

        if (processHandle.IsInvalid)
        {
            applicationId = string.Empty;
            return false;
        }

        uint length = 0;
        WIN32_ERROR result = PInvoke.GetApplicationUserModelId(processHandle, ref length, []);

        if (result != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER || length <= 1 || length > int.MaxValue)
        {
            applicationId = string.Empty;
            return false;
        }

        char[] buffer = new char[length];
        result = PInvoke.GetApplicationUserModelId(processHandle, ref length, buffer);

        if (result != WIN32_ERROR.ERROR_SUCCESS || length <= 1)
        {
            applicationId = string.Empty;
            return false;
        }

        applicationId = $"aumid:{new string(buffer, 0, (int)length - 1).ToUpperInvariant()}";
        return true;
    }

    private static bool TryGetExecutablePath(uint processId, out string executablePath)
    {
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            executablePath = process.MainModule?.FileName ?? string.Empty;
            return !string.IsNullOrWhiteSpace(executablePath);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (Win32Exception)
        {
        }

        executablePath = string.Empty;
        return false;
    }

    private static string NormalizePath(string executablePath) =>
        $"path:{Path.GetFullPath(executablePath).ToUpperInvariant()}";
}
