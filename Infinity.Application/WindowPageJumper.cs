using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class WindowPageJumper(WindowArrowSwitchGesture arrowSwitch,
    WindowArrowMoveGesture arrowMove,
    WindowNumberSwitchGesture numberSwitch,
    WindowNumberMoveGesture numberMove,
    IForegroundWindowSource foregroundWindowSource,
    IWindowStore store,
    IPager pager,
    IWorkspace workspace,
    ILogger<WindowPageJumper> logger) :
    IWindowPageJumper
{
    private const int VirtualKeyRight = 0x27;
    private const int VirtualKey0 = 0x30;

    public void Start()
    {
        arrowSwitch.Invoked += HandleArrowSwitchGesture;
        arrowMove.Invoked += HandleArrowMoveGesture;
        numberSwitch.Invoked += HandleNumberSwitchGesture;
        numberMove.Invoked += HandleNumberMoveGesture;
    }

    public void Stop()
    {
        arrowSwitch.Invoked -= HandleArrowSwitchGesture;
        arrowMove.Invoked -= HandleArrowMoveGesture;
        numberSwitch.Invoked -= HandleNumberSwitchGesture;
        numberMove.Invoked -= HandleNumberMoveGesture;
    }

    private void HandleArrowSwitchGesture(WindowArrowSwitchEventArgs args) => HandleArrow(args.VirtualKeyCode, moveWindow: false);

    private void HandleArrowMoveGesture(WindowArrowMoveEventArgs args) => HandleArrow(args.VirtualKeyCode, moveWindow: true);

    private void HandleNumberSwitchGesture(WindowNumberSwitchEventArgs args) => HandleNumber(args.VirtualKeyCode, moveWindow: false);

    private void HandleNumberMoveGesture(WindowNumberMoveEventArgs args) => HandleNumber(args.VirtualKeyCode, moveWindow: true);

    private void HandleArrow(int virtualKeyCode, bool moveWindow)
    {
        JumpDirection direction = virtualKeyCode == VirtualKeyRight ? JumpDirection.Right : JumpDirection.Left;
        SwitchPage(direction, moveWindow);
    }

    private void HandleNumber(int virtualKeyCode, bool moveWindow)
    {
        if (virtualKeyCode == VirtualKey0)
        {
            return;
        }

        SwitchToPage(virtualKeyCode - VirtualKey0 - 1, moveWindow);
    }

    private bool IsWithinBounds(int targetPage) =>
        targetPage >= 0 && (pager.MaxPages is null || targetPage < pager.MaxPages.Value);

    private void SwitchPage(JumpDirection direction, bool moveWindow)
    {
        if (moveWindow)
        {
            JumpForegroundWindow(direction);
            return;
        }

        int targetPage = pager.CurrentPage + (direction == JumpDirection.Right ? 1 : -1);

        if (!IsWithinBounds(targetPage))
        {
            logger.LogDebug("Page switch blocked at boundary (target {Page})", targetPage);
            return;
        }

        pager.NavigateToPage(targetPage);
    }

    private void SwitchToPage(int targetPage, bool moveWindow)
    {
        if (!IsWithinBounds(targetPage))
        {
            logger.LogDebug("Page switch ignored — target {Page} out of bounds", targetPage);
            return;
        }

        if (moveWindow)
        {
            JumpForegroundWindowToPage(targetPage);
            return;
        }

        pager.NavigateToPage(targetPage);
    }

    private void JumpForegroundWindow(JumpDirection direction)
    {
        nint windowHandle = foregroundWindowSource.GetForegroundWindow();

        if (!store.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            logger.LogDebug("Window jump ignored — foreground window is not tracked");
            return;
        }

        int currentPage = (int)Math.Floor(trackedWindow.CanvasX / (double)workspace.Width);
        int targetPage = currentPage + (direction == JumpDirection.Right ? 1 : -1);

        if (!IsWithinBounds(targetPage))
        {
            logger.LogDebug("Window jump blocked at boundary ({Handle}, target {Page})", windowHandle, targetPage);
            return;
        }

        ApplyPageJump(windowHandle, trackedWindow, currentPage, targetPage);
    }

    private void JumpForegroundWindowToPage(int targetPage)
    {
        nint windowHandle = foregroundWindowSource.GetForegroundWindow();

        if (!store.TryGet(windowHandle, out TrackedWindow trackedWindow))
        {
            logger.LogDebug("Page jump ignored — foreground window is not tracked");
            return;
        }

        int currentPage = (int)Math.Floor(trackedWindow.CanvasX / (double)workspace.Width);

        ApplyPageJump(windowHandle, trackedWindow, currentPage, targetPage);
    }

    private void ApplyPageJump(nint windowHandle, TrackedWindow trackedWindow, int currentPage, int targetPage)
    {
        if (targetPage == currentPage)
        {
            return;
        }

        int delta = (targetPage - currentPage) * workspace.Width;

        trackedWindow.CanvasX += delta;
        store.NotifyChanged(windowHandle);

        logger.LogInformation("Window {Handle} jumped to page {Page}", windowHandle, targetPage);

        pager.NavigateToPage(targetPage);
    }
}