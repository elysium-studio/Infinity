using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowDragGuard(IWindowEventListener listener,
    IModifierKeyState modifierKeyState,
    IDispatcher dispatcher,
    ILogger<WindowDragGuard> logger) :
    IWindowDragGuard
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
    private const uint WmCancelMode = 0x001Fu;
    private const uint WmSysCommand = 0x0112u;
    private const nuint ScDragMove = 0xF012u;
    private const int VkLeftButton = 0x01;
    private const int VkRightButton = 0x02;
    private const int KeyDownMask = 0x8000;
    private const SET_WINDOW_POS_FLAGS SwpNoSize = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
    private const SET_WINDOW_POS_FLAGS SwpNoZOrder = SET_WINDOW_POS_FLAGS.SWP_NOZORDER;
    private const SET_WINDOW_POS_FLAGS SwpNoActivate = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
    private const SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS SpiUpdateFlags = (SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)(SpiUpdateIniFile | SpiSendChange);

    private static readonly string RecoveryFlagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Elysium", "Infinity", "windowArranging.flag");

    private readonly object syncRoot = new();
    private readonly HashSet<nint> draggingWindows = [];
    private readonly HashSet<nint> resizingWindows = [];
    private readonly Dictionary<nint, RECT> dragStartBounds = new();

    private Timer? modifierPollTimer;
    private WindowArrangingState windowArrangingState;
    private bool isModifierDown;
    private bool dragRestartRequired;
    private bool isStarted;
    private int modifierPollVersion;

    public event Action? HoldStarted;

    public bool IsDragging(nint windowHandle)
    {
        lock (syncRoot)
        {
            return draggingWindows.Contains(windowHandle);
        }
    }

    public bool IsAnyDragging
    {
        get
        {
            lock (syncRoot)
            {
                return draggingWindows.Count > 0;
            }
        }
    }

    public bool IsAnyMoving
    {
        get
        {
            lock (syncRoot)
            {
                return draggingWindows.Any(windowHandle => !resizingWindows.Contains(windowHandle));
            }
        }
    }

    public nint DraggingWindow
    {
        get
        {
            lock (syncRoot)
            {
                return draggingWindows.FirstOrDefault(0);
            }
        }
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

        listener.DragStarted += HandleDragStarted;
        listener.DragEnded += HandleDragEnded;
        modifierKeyState.StateChanged += HandleModifierStateChanged;
        AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

        nint[] restartWindowHandles;

        lock (syncRoot)
        {
            UpdateModifierState();
            restartWindowHandles = UpdateWindowArranging();
        }

        RestartDrags(restartWindowHandles);
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

        listener.DragStarted -= HandleDragStarted;
        listener.DragEnded -= HandleDragEnded;
        modifierKeyState.StateChanged -= HandleModifierStateChanged;
        AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
        AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;

        lock (syncRoot)
        {
            StopModifierPolling();
            draggingWindows.Clear();
            resizingWindows.Clear();
            dragStartBounds.Clear();
            isModifierDown = false;
            dragRestartRequired = false;
            RestoreWindowArranging();
        }
    }

    private void HandleProcessExit(object? sender, EventArgs args)
    {
        RestoreWindowArrangingForShutdown();
    }

    private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        RestoreWindowArrangingForShutdown();
    }

    private void RestoreWindowArrangingForShutdown()
    {
        lock (syncRoot)
        {
            StopModifierPolling();
            draggingWindows.Clear();
            resizingWindows.Clear();
            dragStartBounds.Clear();
            isModifierDown = false;
            dragRestartRequired = false;
            RestoreWindowArranging();
        }
    }

    private void HandleModifierStateChanged(bool isDown)
    {
        nint[] restartWindowHandles;

        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            ApplyModifierState(isDown || modifierKeyState.IsActive);
            restartWindowHandles = UpdateWindowArranging();
            StopModifierPollingIfIdle();
        }

        RestartDrags(restartWindowHandles);
    }

    private void HandleDragStarted(nint windowHandle)
    {
        nint[] restartWindowHandles;

        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            draggingWindows.Add(windowHandle);
            resizingWindows.Remove(windowHandle);

            if (PInvoke.GetWindowRect(new HWND(windowHandle), out RECT bounds))
            {
                dragStartBounds[windowHandle] = bounds;
            }
            else
            {
                dragStartBounds.Remove(windowHandle);
            }

            StartModifierPolling();
            UpdateModifierState();
            restartWindowHandles = UpdateWindowArranging();
        }

        RestartDrags(restartWindowHandles);
        HoldStarted?.Invoke();
    }

    private void HandleDragEnded(nint windowHandle)
    {
        nint[] restartWindowHandles;

        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            draggingWindows.Remove(windowHandle);
            resizingWindows.Remove(windowHandle);
            dragStartBounds.Remove(windowHandle);
            UpdateModifierState();
            restartWindowHandles = UpdateWindowArranging();
            StopModifierPollingIfIdle();
        }

        RestartDrags(restartWindowHandles);
    }

    private void HandleModifierPollTimer(object? state)
    {
        if (state is not int version)
        {
            return;
        }

        dispatcher.Dispatch(() => HandleModifierPollTick(version));
    }

    private void HandleModifierPollTick(int version)
    {
        nint[] restartWindowHandles;

        lock (syncRoot)
        {
            if (!isStarted || version != modifierPollVersion)
            {
                return;
            }

            UpdateModifierState();
            ClearStaleDragState();
            restartWindowHandles = UpdateWindowArranging();

            if (draggingWindows.Count > 0)
            {
                DetectResizing();
            }

            StopModifierPollingIfIdle();
        }

        RestartDrags(restartWindowHandles);
    }

    private void ClearStaleDragState()
    {
        if (draggingWindows.Count == 0)
        {
            return;
        }

        if (IsMouseButtonDown())
        {
            return;
        }

        draggingWindows.Clear();
        resizingWindows.Clear();
        dragStartBounds.Clear();
        dragRestartRequired = false;
    }

    private void StopModifierPollingIfIdle()
    {
        if (draggingWindows.Count == 0 && !isModifierDown)
        {
            StopModifierPolling();
        }
    }

    private void DetectResizing()
    {
        foreach (nint windowHandle in draggingWindows)
        {
            if (resizingWindows.Contains(windowHandle))
            {
                continue;
            }

            if (!dragStartBounds.TryGetValue(windowHandle, out RECT startBounds))
            {
                continue;
            }

            if (!PInvoke.GetWindowRect(new HWND(windowHandle), out RECT currentBounds))
            {
                continue;
            }

            int startWidth = startBounds.right - startBounds.left;
            int startHeight = startBounds.bottom - startBounds.top;
            int currentWidth = currentBounds.right - currentBounds.left;
            int currentHeight = currentBounds.bottom - currentBounds.top;

            if (currentWidth != startWidth || currentHeight != startHeight)
            {
                resizingWindows.Add(windowHandle);
            }
        }
    }

    private void StartModifierPolling()
    {
        if (modifierPollTimer != null)
        {
            return;
        }

        modifierPollVersion++;

        int version = modifierPollVersion;

        modifierPollTimer = new Timer(HandleModifierPollTimer, version, TimeSpan.Zero, TimeSpan.FromMilliseconds(10));
    }

    private void StopModifierPolling()
    {
        modifierPollVersion++;
        modifierPollTimer?.Dispose();
        modifierPollTimer = null;
    }

    private void UpdateModifierState()
    {
        ApplyModifierState(modifierKeyState.IsActive);
    }

    private void ApplyModifierState(bool isDown)
    {
        if (!isModifierDown && isDown && draggingWindows.Count > 0)
        {
            dragRestartRequired = true;
        }

        isModifierDown = isDown;
    }

    private nint[] UpdateWindowArranging()
    {
        if (isModifierDown)
        {
            bool disabledOrAlreadyDisabled = DisableWindowArranging();

            if (dragRestartRequired)
            {
                dragRestartRequired = false;

                if (disabledOrAlreadyDisabled)
                {
                    return draggingWindows
                        .Where(windowHandle => !resizingWindows.Contains(windowHandle))
                        .ToArray();
                }
            }

            return [];
        }

        dragRestartRequired = false;
        RestoreWindowArranging();
        return [];
    }

    private void RestartDrags(nint[] windowHandles)
    {
        foreach (nint windowHandle in windowHandles)
        {
            RestartDrag(windowHandle);
        }
    }

    private void RestartDrag(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);

        if (!PInvoke.GetWindowRect(hwnd, out RECT bounds))
        {
            logger.LogWarning("Could not read window bounds for drag restart. Error={Error}", Marshal.GetLastWin32Error());
            return;
        }

        PInvoke.SendMessage(hwnd, WmCancelMode, new WPARAM(0), new LPARAM(0));
        PInvoke.SetWindowPos(hwnd, HWND.Null, bounds.left, bounds.top, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
        PInvoke.PostMessage(hwnd, WmSysCommand, new WPARAM(ScDragMove), new LPARAM(0));
    }

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

    private bool DisableWindowArranging()
    {
        if (windowArrangingState != WindowArrangingState.None)
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
            windowArrangingState = WindowArrangingState.LeaveDisabled;
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

        windowArrangingState = WindowArrangingState.RestoreEnabled;
        return true;
    }

    private void RestoreWindowArranging()
    {
        if (windowArrangingState == WindowArrangingState.None)
        {
            return;
        }

        if (windowArrangingState == WindowArrangingState.RestoreEnabled)
        {
            if (!WriteWindowArranging(true))
            {
                logger.LogWarning("Could not restore window arranging. Error={Error}", Marshal.GetLastWin32Error());
                return;
            }
        }

        windowArrangingState = WindowArrangingState.None;
        DeleteRecoveryFlag();
    }

    private unsafe bool ReadWindowArranging(out bool enabled)
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

    private unsafe bool WriteWindowArranging(bool enabled)
    {
        int desired = enabled ? 1 : 0;

        return PInvoke.SystemParametersInfo(
            (SYSTEM_PARAMETERS_INFO_ACTION)SpiSetWinArranging,
            enabled ? 1u : 0u,
            &desired,
            SpiUpdateFlags);
    }

    private static bool IsMouseButtonDown()
    {
        return (PInvoke.GetAsyncKeyState(VkLeftButton) & KeyDownMask) != 0 ||
            (PInvoke.GetAsyncKeyState(VkRightButton) & KeyDownMask) != 0;
    }
}