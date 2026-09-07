using System;
using System.Diagnostics;
using System.Threading;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Infinity.Shell.WinUI;

public sealed class DesktopContentDragController
{
    private readonly ContentDragProbe probe = new();
    private readonly DesktopContentDragNavigation navigation = new();
    private readonly DesktopScrollPreviewView preview;
    private readonly DesktopOverviewView overlay;
    private readonly IModifierKeyState modifiers;
    private readonly IWindowDragGuard dragGuard;
    private readonly ILogger<DesktopContentDragController> logger;
    private readonly DispatcherQueueTimer timer;
    private readonly DispatcherQueue dispatcher;
    private long startedAt;
    private volatile bool probing;
    private int probeStartQueued;
    private bool handingOff;
    private volatile bool isActive;

    public DesktopContentDragController(
        DesktopOverviewView overlay,
        DesktopScrollPreviewView preview,
        IModifierKeyState modifiers,
        IPointerInputSource pointer,
        IWindowDragGuard dragGuard,
        ILogger<DesktopContentDragController> logger)
    {
        this.overlay = overlay;
        this.preview = preview;
        this.modifiers = modifiers;
        this.dragGuard = dragGuard;
        this.logger = logger;
        dispatcher = overlay.DispatcherQueue;
        timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(32);
        timer.Tick += HandleTick;
        modifiers.StateChanged += HandleModifiersChanged;
        pointer.CursorMoved += HandleCursorMoved;
    }

    public bool IsActive => isActive;

    private void HandleCursorMoved(int x, int y)
    {
        if (probing || IsActive || overlay.IsOverlayVisible || !ContentDragProbe.IsButtonDown || Interlocked.Exchange(ref probeStartQueued, 1) != 0)
        {
            return;
        }

        if (!dispatcher.TryEnqueue(() =>
        {
            Interlocked.Exchange(ref probeStartQueued, 0);
            TryStart();
        }))
        {
            Interlocked.Exchange(ref probeStartQueued, 0);
        }
    }

    private void HandleModifiersChanged(bool active)
    {
        if (active && ContentDragProbe.IsButtonDown)
        {
            dispatcher.TryEnqueue(() =>
            {
                if (IsActive)
                {
                    navigation.Update(true, DesktopContentDragTarget.None);
                }
                else
                {
                    TryStart();
                }
            });
        }
        else if (!active && IsActive)
        {
            dispatcher.TryEnqueue(SelectReleasedTarget);
        }
    }

    private void SelectReleasedTarget()
    {
        if (!IsActive || handingOff)
        {
            return;
        }

        try
        {
            if (!overlay.CanContinueContentDrag || !ContentDragProbe.IsButtonDown || !preview.TryGetContentDragTarget(out DesktopContentDragTarget target))
            {
                Cancel();
                return;
            }

            DesktopContentDragTarget selected = navigation.Update(false, target);
            if (selected.Page < 0)
            {
                return;
            }

            handingOff = true;
            preview.CompleteContentDragSelection();
            if (selected.Window == 0)
            {
                overlay.ViewModel.SelectPage(selected.Page);
            }
            else
            {
                overlay.ViewModel.ActivateWindow(selected.Window);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not select the content drag destination.");
            handingOff = false;
            Cancel();
        }
    }

    private void TryStart()
    {
        if (probing || IsActive || overlay.IsOpen || dragGuard.IsAnyDragging || !modifiers.IsActive || !ContentDragProbe.IsButtonDown)
        {
            return;
        }

        try
        {
            probe.Start();
            probing = true;
            startedAt = Stopwatch.GetTimestamp();
            timer.Start();
        }
        catch (Exception exception)
        {
            probe.Dispose();
            logger.LogWarning(exception, "Could not detect the external content drag.");
        }
    }

    private void HandleTick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            Advance();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Content drag navigation stopped.");
            Cancel();
        }
    }

    private void Advance()
    {
        if (probing)
        {
            ContentDragKind kind = probe.Poll();
            if (dragGuard.IsAnyDragging || !ContentDragProbe.IsButtonDown || !modifiers.IsActive || Stopwatch.GetElapsedTime(startedAt) > TimeSpan.FromMilliseconds(800))
            {
                Stop();
                return;
            }
            if (kind != ContentDragKind.None)
            {
                probe.Dispose();
                probing = false;
                isActive = true;
                navigation.Update(true, DesktopContentDragTarget.None);
                preview.SetContentDragEnabled(true);
                overlay.ViewModel.OpenForContentDrag();
            }
            return;
        }

        if (!IsActive)
        {
            return;
        }
        if (!overlay.CanContinueContentDrag)
        {
            Stop();
            return;
        }
        if (!ContentDragProbe.IsButtonDown)
        {
            Cancel();
            return;
        }
        if (handingOff || !preview.IsContentDragReady)
        {
            return;
        }

        preview.TryGetContentDragTarget(out _);
    }

    public void Cancel()
    {
        bool dismiss = IsActive && !handingOff;
        Stop();
        if (dismiss && overlay.IsOpen)
        {
            overlay.ViewModel.DismissDesktopPreview();
        }
    }

    public void Stop()
    {
        timer.Stop();
        probe.Dispose();
        if (IsActive)
        {
            preview.SetContentDragEnabled(false);
        }
        probing = false;
        isActive = false;
        handingOff = false;
        navigation.Reset();
    }
}
