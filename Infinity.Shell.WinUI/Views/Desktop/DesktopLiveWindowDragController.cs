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
    private bool handingOff;
    private nint candidateWindow;
    private volatile bool isActive;
    private int pointerRefreshQueued;
    private int? releasedThrowDirection;
    private DesktopSnapPlacement? throwOrigin;

    public DesktopLiveWindowDragController(
        DesktopOverviewView overlay,
        DesktopScrollPreviewView preview,
        IModifierKeyState modifiers,
        IWindowEventListener events,
        IPointerInputSource pointer,
        IWindowDragGuard dragGuard,
        IWindowStore windows,
        IWindowGeometryReader geometry,
        ITrackedWindowDragController dragController,
        ILogger<DesktopLiveWindowDragController> logger)
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
        events.DragStarted += HandleDragStarted;
        events.DragEnded += HandleDragEnded;
        events.WindowLocationChanged += HandleLocationChanged;
        modifiers.StateChanged += HandleModifiersChanged;
        pointer.CursorMoved += HandleCursorMoved;
    }

    public bool IsActive => isActive;

    private void HandleCursorMoved(int x, int y)
    {
        if (!IsActive || Interlocked.Exchange(ref pointerRefreshQueued, 1) != 0)
        {
            return;
        }

        if (!dispatcher.TryEnqueue(RefreshDragPointer))
        {
            Interlocked.Exchange(ref pointerRefreshQueued, 0);
        }
    }

    private void RefreshDragPointer()
    {
        Interlocked.Exchange(ref pointerRefreshQueued, 0);
        if (!IsActive || !overlay.CanContinueContentDrag)
        {
            return;
        }

        try
        {
            if (preview.DragFrames.TryRead(out DesktopWindowDragFrame frame))
            {
                preview.UpdateLiveWindowDragPosition(frame.ScreenX, frame.ScreenY);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not update the live window drag position.");
            Cancel();
        }
    }

    private void HandleDragStarted(nint handle) => dispatcher.TryEnqueue(() =>
    {
        if (IsActive)
        {
            return;
        }

        gesture.Reset();
        throwOrigin = null;
        Volatile.Write(ref candidateWindow, 0);
        if (windows.TryGet(handle, out TrackedWindow? window) && TryRead(handle, out DesktopSnapPlacement bounds))
        {
            throwOrigin = new DesktopWindowFrameGeometry(geometry).GetVisiblePlacement(window);
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
            if (!NativeDragPointer.IsButtonDown)
            {
                CompleteDrop();
            }
            else
            {
                Cancel();
            }
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
            if (!NativeDragPointer.TryGetPosition(out int pointerX, out int pointerY) || !TryReadVisible(gesture.Window, out DesktopSnapPlacement bounds) || !DesktopWindowDragAnchor.TryCreate(pointerX, pointerY, bounds, out DesktopWindowDragAnchor anchor))
            {
                return;
            }

            if (!dragController.Begin(gesture.Window))
            {
                return;
            }

            isActive = true;
            navigation.Update(true, DesktopContentDragTarget.None);
            preview.BeginLiveWindowDrag(gesture.Window, anchor, pointerX, pointerY, throwOrigin);
            preview.DragFrames.Updated += HandleDragFrame;
            overlay.ViewModel.OpenForContentDrag();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not open the live window drag overview.");
            Cancel();
        }
    }

    private void HandleDragFrame(DesktopWindowDragFrame frame)
    {
        try
        {
            if (!IsActive)
            {
                return;
            }

            if (!overlay.CanContinueContentDrag)
            {
                Stop();
            }
            else
            {
                if (handingOff)
                {
                    preview.UpdateLiveWindowDragPosition(frame.ScreenX, frame.ScreenY);
                    return;
                }

                preview.TryGetLiveWindowDragTarget(frame.ScreenX, frame.ScreenY, out _);

                if (!windows.TryGet(gesture.Window, out _))
                {
                    Cancel();
                    return;
                }

                if (!frame.IsButtonDown)
                {
                    releasedThrowDirection ??= preview.ReleaseLiveWindowThrowGesture();
                    preview.StopLiveWindowDragScroll();
                    if (!dragGuard.IsDragging(gesture.Window))
                    {
                        CompleteDrop();
                    }

                    return;
                }

                if (!dragGuard.IsDragging(gesture.Window))
                {
                    Cancel();
                    return;
                }

                preview.UpdateLiveWindowDragScroll();
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

    private void CompleteDrop()
    {
        if (!IsActive || handingOff)
        {
            return;
        }

        try
        {
            if (!overlay.CanContinueContentDrag || !NativeDragPointer.TryGetPosition(out int pointerX, out int pointerY))
            {
                Cancel();
                return;
            }

            preview.TryGetLiveWindowDragTarget(pointerX, pointerY, out _);
            releasedThrowDirection ??= preview.ReleaseLiveWindowThrowGesture();
            if (!preview.TryCompleteLiveWindowDrop(releasedThrowDirection.Value, out int page))
            {
                Cancel();
                return;
            }

            handingOff = true;
            dragController.End(gesture.Window);
            preview.EndLiveWindowDrag();
            preview.CompleteContentDragSelection();
            overlay.ViewModel.SelectPage(page);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not place the live window drag inside its destination page.");
            handingOff = false;
            Cancel();
        }
    }

    private void SelectReleasedPage()
    {
        if (handingOff)
        {
            return;
        }

        try
        {
            if (!NativeDragPointer.IsButtonDown)
            {
                if (!dragGuard.IsDragging(gesture.Window))
                {
                    CompleteDrop();
                }

                return;
            }

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
        preview.DragFrames.Updated -= HandleDragFrame;
        if (IsActive)
        {
            dragController.End(gesture.Window);
            preview.EndLiveWindowDrag();
        }

        isActive = false;
        releasedThrowDirection = null;
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

    private bool TryReadVisible(nint handle, out DesktopSnapPlacement bounds)
    {
        if (geometry.TryReadVisibleGeometry(handle, out int x, out int y, out int width, out int height))
        {
            bounds = new(x, y, width, height);
            return true;
        }

        return TryRead(handle, out bounds);
    }
}
