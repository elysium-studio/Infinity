using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed class WindowArrangingController(ILogger<WindowArrangingController> logger)
{
    private enum WindowArrangingState
    {
        None,
        RestoreEnabled,
        LeaveDisabled
    }

    private const uint SpiGetWinArranging = 0x0082u;
    private const uint SpiSetWinArranging = 0x0083u;
    private const uint SpiUpdateIniFile = 0x0001u;
    private const uint SpiSendChange = 0x0002u;
    private const SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS SpiUpdateFlags = (SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)(SpiUpdateIniFile | SpiSendChange);

    private static readonly string RecoveryFlagPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Elysium",
        "Infinity",
        "windowArranging.flag");

    private readonly Lock syncRoot = new();
    private WindowArrangingState state;
    private bool isStarted;

    public void Start()
    {
        lock (syncRoot)
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
        }

        RecoverFromCrashedSession();
        AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
    }

    public void Stop()
    {
        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
        }

        AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
        AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
        Restore();
    }

    public bool Disable()
    {
        lock (syncRoot)
        {
            if (state != WindowArrangingState.None)
            {
                return true;
            }

            if (!ReadWindowArranging(out bool enabled))
            {
                logger.LogWarning("Could not read window arranging setting. Error={Error}", Marshal.GetLastWin32Error());
                return false;
            }

            if (!enabled)
            {
                state = WindowArrangingState.LeaveDisabled;
                return true;
            }

            if (!WriteRecoveryFlag())
            {
                return false;
            }

            if (!WriteWindowArranging(false))
            {
                DeleteRecoveryFlag();
                logger.LogWarning("Could not disable window arranging. Error={Error}", Marshal.GetLastWin32Error());
                return false;
            }

            state = WindowArrangingState.RestoreEnabled;
            return true;
        }
    }

    public void Restore()
    {
        lock (syncRoot)
        {
            if (state == WindowArrangingState.None)
            {
                return;
            }

            if (state == WindowArrangingState.RestoreEnabled && !WriteWindowArranging(true))
            {
                logger.LogWarning("Could not restore window arranging. Error={Error}", Marshal.GetLastWin32Error());
                return;
            }

            state = WindowArrangingState.None;
            DeleteRecoveryFlag();
        }
    }

    private void HandleProcessExit(object? sender, EventArgs args) => Restore();

    private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args) => Restore();

    private void RecoverFromCrashedSession()
    {
        string? recoveredValue = null;

        try
        {
            if (File.Exists(RecoveryFlagPath))
            {
                recoveredValue = File.ReadAllText(RecoveryFlagPath).Trim();
            }
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Could not read window arranging recovery flag.");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Could not read window arranging recovery flag.");
        }

        if (recoveredValue is null)
        {
            return;
        }

        if (recoveredValue == "1")
        {
            logger.LogWarning("Window arranging was left disabled by a previous session that likely crashed. Restoring Enabled=True.");

            if (!WriteWindowArranging(true))
            {
                logger.LogWarning("Could not recover window arranging after previous crash. Error={Error}", Marshal.GetLastWin32Error());
                return;
            }
        }

        DeleteRecoveryFlag();
    }

    private bool WriteRecoveryFlag()
    {
        try
        {
            string? directory = Path.GetDirectoryName(RecoveryFlagPath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(RecoveryFlagPath, "1");
            return true;
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Could not write window arranging recovery flag.");
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Could not write window arranging recovery flag.");
            return false;
        }
    }

    private void DeleteRecoveryFlag()
    {
        try
        {
            if (File.Exists(RecoveryFlagPath))
            {
                File.Delete(RecoveryFlagPath);
            }
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Could not delete window arranging recovery flag.");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Could not delete window arranging recovery flag.");
        }
    }

    private static unsafe bool ReadWindowArranging(out bool enabled)
    {
        int value = 0;

        if (!PInvoke.SystemParametersInfo((SYSTEM_PARAMETERS_INFO_ACTION)SpiGetWinArranging, 0, &value, 0))
        {
            enabled = false;
            return false;
        }

        enabled = value != 0;
        return true;
    }

    private static unsafe bool WriteWindowArranging(bool enabled)
    {
        int desired = enabled ? 1 : 0;

        return PInvoke.SystemParametersInfo(
            (SYSTEM_PARAMETERS_INFO_ACTION)SpiSetWinArranging,
            enabled ? 1u : 0u,
            &desired,
            SpiUpdateFlags);
    }
}
