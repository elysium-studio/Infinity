using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

internal sealed class Win32WindowArrangingSystem :
    IWindowArrangingSystem
{
    private const uint SpiGetWinArranging = 0x0082u;
    private const uint SpiSetWinArranging = 0x0083u;
    private const uint SpiUpdateIniFile = 0x0001u;
    private const uint SpiSendChange = 0x0002u;
    private const SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS SpiUpdateFlags = (SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)(SpiUpdateIniFile | SpiSendChange);

    public unsafe bool TryRead(out bool enabled, out int error)
    {
        int value = 0;

        if (!PInvoke.SystemParametersInfo((SYSTEM_PARAMETERS_INFO_ACTION)SpiGetWinArranging, 0, &value, 0))
        {
            enabled = false;
            error = Marshal.GetLastWin32Error();
            return false;
        }

        enabled = value != 0;
        error = 0;
        return true;
    }

    public unsafe bool TryWrite(bool enabled, out int error)
    {
        int desired = enabled ? 1 : 0;
        bool success = PInvoke.SystemParametersInfo((SYSTEM_PARAMETERS_INFO_ACTION)SpiSetWinArranging,
            enabled ? 1u : 0u,
            &desired,
            SpiUpdateFlags);

        error = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }
}
