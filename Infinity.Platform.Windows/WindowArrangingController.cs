using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public sealed class WindowArrangingController
{
    private enum WindowArrangingState
    {
        None,
        RestoreEnabled,
        LeaveDisabled
    }

    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromMilliseconds(250);
    private static readonly long WatchdogLeaseTimeoutTicks = Stopwatch.Frequency * 2;

    private static readonly string DefaultRecoveryFlagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Elysium",
        "Infinity",
        "windowArranging.flag");

    private readonly ILogger<WindowArrangingController> logger;
    private readonly IWindowArrangingSystem system;
    private readonly Func<long> getTimestamp;
    private readonly long watchdogLeaseTimeoutTicks;
    private readonly string recoveryFlagPath;
    private readonly Lock syncRoot = new();

    private Timer? watchdogTimer;
    private WindowArrangingState state;
    private long lastDisableHeartbeatTimestamp;
    private bool watchdogRestoreFailureLogged;
    private bool isStarted;

    public WindowArrangingController(ILogger<WindowArrangingController> logger) :
        this(logger,
            new Win32WindowArrangingSystem(),
            Stopwatch.GetTimestamp,
            WatchdogLeaseTimeoutTicks,
            DefaultRecoveryFlagPath)
    {
    }

    internal WindowArrangingController(ILogger<WindowArrangingController> logger,
        IWindowArrangingSystem system,
        Func<long> getTimestamp,
        long watchdogLeaseTimeoutTicks,
        string recoveryFlagPath)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(watchdogLeaseTimeoutTicks);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryFlagPath);

        this.logger = logger;
        this.system = system;
        this.getTimestamp = getTimestamp;
        this.watchdogLeaseTimeoutTicks = watchdogLeaseTimeoutTicks;
        this.recoveryFlagPath = recoveryFlagPath;
    }

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

        lock (syncRoot)
        {
            watchdogTimer = new Timer(HandleWatchdogTick,
                null,
                WatchdogInterval,
                WatchdogInterval);
        }
    }

    public void Stop()
    {
        Timer? timer;

        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            timer = watchdogTimer;
            watchdogTimer = null;
        }

        timer?.Dispose();
        AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
        AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
        Restore();
    }

    public bool Disable()
    {
        lock (syncRoot)
        {
            if (state == WindowArrangingState.RestoreEnabled)
            {
                RefreshWatchdogHeartbeat();
                return true;
            }

            if (state == WindowArrangingState.LeaveDisabled)
            {
                return true;
            }

            if (!system.TryRead(out bool enabled, out int error))
            {
                logger.LogWarning("Could not read window arranging setting. Error={Error}", error);
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

            if (!system.TryWrite(false, out error))
            {
                DeleteRecoveryFlag();
                logger.LogWarning("Could not disable window arranging. Error={Error}", error);
                return false;
            }

            state = WindowArrangingState.RestoreEnabled;
            RefreshWatchdogHeartbeat();
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

            if (state == WindowArrangingState.RestoreEnabled && !system.TryWrite(true, out int error))
            {
                logger.LogWarning("Could not restore window arranging. Error={Error}", error);
                return;
            }

            CompleteRestore();
        }
    }

    private void HandleProcessExit(object? sender, EventArgs args) => Restore();

    private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args) => Restore();

    private void HandleWatchdogTick(object? state) => RunWatchdog();

    internal void RunWatchdog()
    {
        lock (syncRoot)
        {
            if (state != WindowArrangingState.RestoreEnabled)
            {
                return;
            }

            long elapsedTicks = getTimestamp() - lastDisableHeartbeatTimestamp;

            if (elapsedTicks < watchdogLeaseTimeoutTicks)
            {
                return;
            }

            if (!system.TryWrite(true, out int error))
            {
                if (!watchdogRestoreFailureLogged)
                {
                    watchdogRestoreFailureLogged = true;
                    logger.LogWarning("Window arranging watchdog could not restore the setting. Error={Error}", error);
                }

                return;
            }

            logger.LogWarning("Window arranging watchdog restored the setting after drag cleanup stopped responding.");
            CompleteRestore();
        }
    }

    private void RefreshWatchdogHeartbeat()
    {
        lastDisableHeartbeatTimestamp = getTimestamp();
        watchdogRestoreFailureLogged = false;
    }

    private void CompleteRestore()
    {
        state = WindowArrangingState.None;
        lastDisableHeartbeatTimestamp = 0;
        watchdogRestoreFailureLogged = false;
        DeleteRecoveryFlag();
    }

    private void RecoverFromCrashedSession()
    {
        string? recoveredValue = null;

        try
        {
            if (File.Exists(recoveryFlagPath))
            {
                recoveredValue = File.ReadAllText(recoveryFlagPath).Trim();
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

            if (!system.TryWrite(true, out int error))
            {
                logger.LogWarning("Could not recover window arranging after previous crash. Error={Error}", error);

                lock (syncRoot)
                {
                    state = WindowArrangingState.RestoreEnabled;
                    lastDisableHeartbeatTimestamp = getTimestamp() - watchdogLeaseTimeoutTicks;
                }

                return;
            }
        }

        DeleteRecoveryFlag();
    }

    private bool WriteRecoveryFlag()
    {
        try
        {
            string? directory = Path.GetDirectoryName(recoveryFlagPath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(recoveryFlagPath, "1");
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
            if (File.Exists(recoveryFlagPath))
            {
                File.Delete(recoveryFlagPath);
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
}

internal interface IWindowArrangingSystem
{
    bool TryRead(out bool enabled, out int error);

    bool TryWrite(bool enabled, out int error);
}

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
