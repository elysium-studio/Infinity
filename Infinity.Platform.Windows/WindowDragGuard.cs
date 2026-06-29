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
    private const uint SpiGetWinArranging = 0x0082u;
    private const uint SpiSetWinArranging = 0x0083u;
    private const uint SpiSendChange = 0x0002u;
    private const uint WmCancelMode = 0x001Fu;
    private const uint WmSysCommand = 0x0112u;
    private const nuint ScDragMove = 0xF012u;
    private const SET_WINDOW_POS_FLAGS SwpNoSize = SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
    private const SET_WINDOW_POS_FLAGS SwpNoZOrder = SET_WINDOW_POS_FLAGS.SWP_NOZORDER;
    private const SET_WINDOW_POS_FLAGS SwpNoActivate = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

    private readonly object syncRoot = new();
    private readonly HashSet<nint> draggingWindows = [];
    private readonly HashSet<nint> resizingWindows = [];
    private readonly Dictionary<nint, RECT> dragStartBounds = new();

    private Timer? modifierPollTimer;
    private bool? previousWindowArranging;
    private bool isModifierDown;
    private bool dragRestartRequired;

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
        listener.DragStarted += HandleDragStarted;
        listener.DragEnded += HandleDragEnded;
        modifierKeyState.StateChanged += HandleModifierStateChanged;

        lock (syncRoot)
        {
            UpdateModifierState();
            UpdateWindowArranging();
        }
    }

    public void Stop()
    {
        listener.DragStarted -= HandleDragStarted;
        listener.DragEnded -= HandleDragEnded;
        modifierKeyState.StateChanged -= HandleModifierStateChanged;

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
        lock (syncRoot)
        {
            ApplyModifierState(isDown || modifierKeyState.IsActive);
            UpdateWindowArranging();
        }
    }

    private void HandleDragStarted(nint windowHandle)
    {
        lock (syncRoot)
        {
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
            UpdateWindowArranging();
        }

        HoldStarted?.Invoke();
    }

    private void HandleDragEnded(nint windowHandle)
    {
        lock (syncRoot)
        {
            draggingWindows.Remove(windowHandle);
            resizingWindows.Remove(windowHandle);
            dragStartBounds.Remove(windowHandle);
            UpdateModifierState();
            UpdateWindowArranging();

            if (draggingWindows.Count == 0)
            {
                StopModifierPolling();
            }
        }
    }

    private void HandleModifierPollTimer(object? state)
    {
        dispatcher.Dispatch(HandleModifierPollTick);
    }

    private void HandleModifierPollTick()
    {
        lock (syncRoot)
        {
            if (draggingWindows.Count == 0)
            {
                StopModifierPolling();
                return;
            }

            UpdateModifierState();
            UpdateWindowArranging();
            DetectResizing();
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
        modifierPollTimer ??= new Timer(HandleModifierPollTimer, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(10));
    }

    private void StopModifierPolling()
    {
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

    private void UpdateWindowArranging()
    {
        if (isModifierDown)
        {
            DisableWindowArranging();

            if (dragRestartRequired)
            {
                dragRestartRequired = false;
                RestartDrags();
            }
        }
        else
        {
            dragRestartRequired = false;
            RestoreWindowArranging();
        }
    }

    private void RestartDrags()
    {
        foreach (nint windowHandle in draggingWindows.ToArray())
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

    private unsafe void DisableWindowArranging()
    {
        if (previousWindowArranging.HasValue)
        {
            return;
        }

        bool enabled = false;

        if (!PInvoke.SystemParametersInfo((SYSTEM_PARAMETERS_INFO_ACTION)SpiGetWinArranging, 0, &enabled, 0))
        {
            logger.LogWarning("Could not read window arranging setting. Error={Error}", Marshal.GetLastWin32Error());
            return;
        }

        previousWindowArranging = enabled;

        bool disabled = false;

        if (!PInvoke.SystemParametersInfo((SYSTEM_PARAMETERS_INFO_ACTION)SpiSetWinArranging, 0, &disabled, (SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)SpiSendChange))
        {
            previousWindowArranging = null;
            logger.LogWarning("Could not disable window arranging. Error={Error}", Marshal.GetLastWin32Error());
        }
    }

    private unsafe void RestoreWindowArranging()
    {
        if (!previousWindowArranging.HasValue)
        {
            return;
        }

        bool enabled = previousWindowArranging.Value;

        if (!PInvoke.SystemParametersInfo((SYSTEM_PARAMETERS_INFO_ACTION)SpiSetWinArranging, enabled ? 1u : 0u, &enabled, (SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)SpiSendChange))
        {
            logger.LogWarning("Could not restore window arranging. Enabled={Enabled}, Error={Error}", enabled, Marshal.GetLastWin32Error());
            return;
        }

        previousWindowArranging = null;
    }
}