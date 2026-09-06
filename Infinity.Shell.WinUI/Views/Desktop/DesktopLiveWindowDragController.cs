using System;
using System.Threading;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Infinity.Shell.WinUI;

public sealed class DesktopLiveWindowDragController
{
    private readonly DesktopLiveWindowDragGesture gesture = new();
    private readonly DesktopContentDragNavigation navigation = new();
    private readonly DesktopOverviewView overlay;
    private readonly DesktopScrollPreviewView preview;
    private readonly IModifierKeyState modifiers;
    private readonly IWindowDragGuard dragGuard;
    private readonly IWindowStore windows;
    private readonly IWindowGeometryReader geometry;
    private readonly ITrackedWindowDragController dragController;
    private readonly ILogger<DesktopLiveWindowDragController> logger;
    private readonly DispatcherQueue dispatcher;
    private readonly DispatcherQueueTimer timer;
    private bool handingOff;
    private nint candidateWindow;
    private volatile bool isActive;

    public DesktopLiveWindowDragController(DesktopOverviewView overlay, DesktopScrollPreviewView preview, IModifierKeyState modifiers, IWindowEventListener events, IWindowDragGuard dragGuard, IWindowStore windows, IWindowGeometryReader geometry, ITrackedWindowDragController dragController, ILogger<DesktopLiveWindowDragController> logger)
    {
        this.overlay = overlay;
        this.preview = preview;
        this.modifiers = modifiers;
        this.dragGuard = dragGuard;
        this.windows = windows;
        this.geometry = geometry;
        this.dragController = dragController;
        this.logger = logger;
        dispatcher = overlay.DispatcherQueue;
        timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(32);
        timer.Tick += HandleTick;
        events.DragStarted += HandleDragStarted;
        events.DragEnded += HandleDragEnded;
        events.WindowLocationChanged += HandleLocationChanged;
        modifiers.StateChanged += HandleModifiersChanged;
    }

    public bool IsActive => isActive;

    private void HandleDragStarted(nint handle) => dispatcher.TryEnqueue(() =>
    {
        if (IsActive)
        {
            return;
        }

        gesture.Reset();
        Volatile.Write(ref candidateWindow, 0);
        if (windows.TryGet(handle, out _) && TryRead(handle, out DesktopSnapPlacement bounds))
        {
            gesture.Begin(handle, bounds);
            Volatile.Write(ref candidateWindow, gesture.Window);
        }
    });

    private void HandleDragEnded(nint handle) => dispatcher.TryEnqueue(() =>
    {
        if (handle != gesture.Window)
        {
            return;
        }

        if (IsActive)
        {
            Cancel();
        }
        else
        {
            gesture.Reset();
            Volatile.Write(ref candidateWindow, 0);
        }
    });

    private void HandleLocationChanged(nint handle)
    {
        if (IsActive || handle != Volatile.Read(ref candidateWindow))
        {
            return;
        }

        dispatcher.TryEnqueue(() =>
        {
            if (!IsActive && gesture.Window == handle && TryRead(handle, out DesktopSnapPlacement bounds))
            {
                gesture.Update(handle, bounds);
                TryStart();
            }
        });
    }

    private void HandleModifiersChanged(bool active) => dispatcher.TryEnqueue(() =>
    {
        if (active)
        {
            TryStart();
        }
        else if (IsActive)
        {
            SelectReleasedPage();
        }
    });

    private void TryStart()
    {
        if (IsActive || !gesture.CanOpen(modifiers.IsActive, NativeDragPointer.IsButtonDown, overlay.IsOpen) || !dragGuard.IsDragging(gesture.Window))
        {
            return;
        }

        try
        {
            if (!dragController.Begin(gesture.Window))
            {
                return;
            }

            isActive = true;
            navigation.Update(true, DesktopContentDragTarget.None);
            preview.SetLiveWindowDragEnabled(true);
            timer.Start();
            overlay.ViewModel.OpenForContentDrag();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not open the live window drag overview.");
            Cancel();
        }
    }

    private void HandleTick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            if (!overlay.CanContinueContentDrag)
            {
                Stop();
            }
            else if (!handingOff)
            {
                if (!windows.TryGet(gesture.Window, out _) || !dragGuard.IsDragging(gesture.Window) || !NativeDragPointer.IsButtonDown)
                {
                    Cancel();
                    return;
                }

                TryGetPage(out _);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Live window drag navigation stopped.");
            Cancel();
        }
    }

    private bool TryGetPage(out DesktopContentDragTarget target)
    {
        target = DesktopContentDragTarget.None;
        return NativeDragPointer.TryGetPosition(out int x, out int y) && preview.TryGetLiveWindowDragTarget(x, y, out target);
    }

    private void SelectReleasedPage()
    {
        if (handingOff)
        {
            return;
        }

        try
        {
            if (!overlay.CanContinueContentDrag || !NativeDragPointer.IsButtonDown || !dragGuard.IsDragging(gesture.Window) || !TryGetPage(out DesktopContentDragTarget target))
            {
                Cancel();
                return;
            }

            DesktopContentDragTarget selected = navigation.Update(false, target);
            if (selected.Page >= 0)
            {
                handingOff = true;
                preview.CompleteContentDragSelection();
                overlay.ViewModel.SelectPage(selected.Page);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not select the live window drag destination.");
            handingOff = false;
            Cancel();
        }
    }

    public void Cancel()
    {
        if (!IsActive || handingOff)
        {
            return;
        }

        handingOff = true;
        preview.CompleteContentDragSelection();
        if (overlay.IsOpen)
        {
            overlay.ViewModel.DismissDesktopPreview();
        }
        else
        {
            Stop();
        }
    }

    public void Stop()
    {
        timer.Stop();
        if (IsActive)
        {
            dragController.End(gesture.Window);
            preview.SetLiveWindowDragEnabled(false);
        }

        isActive = false;
        handingOff = false;
        gesture.Reset();
        Volatile.Write(ref candidateWindow, 0);
        navigation.Reset();
    }

    private bool TryRead(nint handle, out DesktopSnapPlacement bounds)
    {
        bool result = geometry.TryReadGeometry(handle, out int x, out int y, out int width, out int height);
        bounds = new(x, y, width, height);
        return result;
    }
}
