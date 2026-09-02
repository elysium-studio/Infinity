using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewInputController(
    IWindowCollection windowCollection,
    IPager pager,
    DesktopWindowPlacementCoordinator windowPlacementCoordinator,
    DesktopWindowPreviewCollection previews,
    IKeyboardTextTranslator keyboardTextTranslator)
{
    private bool controlKeyDown;
    private bool filterActive;
    private int pageBeforeFilter = -1;
    private bool shiftKeyDown;

    public event Action<nint>? WindowInvoked;

    public void ApplyFilter(string text, bool isRunning)
    {
        bool isActive = !string.IsNullOrWhiteSpace(text);
        nint focusedHandle = previews.SetFilter(text, windowCollection.AllTrackedWindows);

        if (!isRunning)
        {
            filterActive = false;
            pageBeforeFilter = -1;
            return;
        }

        if (!filterActive && isActive)
        {
            pageBeforeFilter = pager.CurrentPage;
        }

        filterActive = isActive;

        if (!isActive)
        {
            RestorePageBeforeFilter();
            return;
        }

        NavigateToWindow(focusedHandle);
    }

    public bool HandleKeyDown(VirtualKey key)
    {
        if (key == VirtualKey.Shift)
        {
            shiftKeyDown = true;
            return false;
        }

        if (key == VirtualKey.Control)
        {
            controlKeyDown = true;
            return false;
        }

        return TryHandleCommand(key, controlKeyDown, shiftKeyDown);
    }

    public void HandleKeyUp(VirtualKey key)
    {
        if (key == VirtualKey.Shift)
        {
            shiftKeyDown = false;
        }
        else if (key == VirtualKey.Control)
        {
            controlKeyDown = false;
        }
    }

    public bool TryHandleGlobalKeyDown(int virtualKeyCode, bool controlDown, bool shiftDown, bool menuDown, bool windowsDown, Action removeLastCharacter, Action<string> appendText, Action requestTextFocus)
    {
        VirtualKey key = (VirtualKey)virtualKeyCode;

        if (TryHandleCommand(key, controlDown && !menuDown, shiftDown))
        {
            return true;
        }

        if (windowsDown || controlDown != menuDown)
        {
            return false;
        }

        if (key == VirtualKey.Back)
        {
            removeLastCharacter();
            requestTextFocus();
            return true;
        }

        string? text = keyboardTextTranslator.Translate(virtualKeyCode);

        if (string.IsNullOrEmpty(text) || text.Any(char.IsControl))
        {
            return false;
        }

        appendText(text);
        requestTextFocus();
        return true;
    }

    public void Reset()
    {
        filterActive = false;
        pageBeforeFilter = -1;
        ResetModifiers();
        previews.SetFilter(string.Empty, windowCollection.AllTrackedWindows);
    }

    public void ResetModifiers()
    {
        shiftKeyDown = false;
        controlKeyDown = false;
    }

    private bool TryHandleCommand(VirtualKey key, bool controlDown, bool shiftDown)
    {
        if (filterActive && key is VirtualKey.Up or VirtualKey.Down)
        {
            NavigateToWindow(previews.SelectNext(key == VirtualKey.Down, windowCollection.AllTrackedWindows));
            return true;
        }

        if (!filterActive && controlDown && key == VirtualKey.A)
        {
            return true;
        }

        if (!filterActive && shiftDown && key is VirtualKey.Left or VirtualKey.Right)
        {
            MoveSelectedWindows(key == VirtualKey.Right ? 1 : -1);
            return true;
        }

        if (!filterActive && !controlDown && !shiftDown && key is VirtualKey.Left or VirtualKey.Right)
        {
            NavigateToPage(pager.CurrentPage + (key == VirtualKey.Right ? 1 : -1));
            return true;
        }

        if (!filterActive && !controlDown && !shiftDown && key is VirtualKey.Up or VirtualKey.Down)
        {
            previews.SelectWithin(GetWindowsOnPage(pager.CurrentPage), key == VirtualKey.Down);
            return true;
        }

        if (!filterActive && !controlDown && !shiftDown && TryGetPageFromNumberKey(key, out int page))
        {
            NavigateToPage(page);
            return true;
        }

        if (key == VirtualKey.Tab)
        {
            NavigateToWindow(previews.SelectNext(!shiftDown, windowCollection.AllTrackedWindows));
            return true;
        }

        if (key != VirtualKey.Enter)
        {
            return false;
        }

        nint handle = filterActive ? previews.GetSelectedMatchingWindow(windowCollection.AllTrackedWindows) : previews.GetFocusedHandle();

        if (handle == 0 && !filterActive)
        {
            handle = previews.SelectFirst(GetWindowsOnPage(pager.CurrentPage));
        }

        if (handle == 0)
        {
            return false;
        }

        WindowInvoked?.Invoke(handle);
        return true;
    }

    private void MoveSelectedWindows(int pageDelta)
    {
        IReadOnlyCollection<nint> selectedHandles = previews.GetSelectedHandles();

        if (selectedHandles.Count == 0)
        {
            nint focusedHandle = previews.GetFocusedHandle();

            if (focusedHandle == 0)
            {
                focusedHandle = previews.SelectFirst(GetWindowsOnPage(pager.CurrentPage));
            }

            if (focusedHandle != 0)
            {
                selectedHandles = [focusedHandle];
            }
        }

        windowPlacementCoordinator.MoveByPages(selectedHandles, pageDelta, pager.MaxPages);
    }

    private void NavigateToPage(int page)
    {
        if (page < 0 || pager.MaxPages.HasValue && page >= pager.MaxPages.Value)
        {
            return;
        }

        pager.NavigateToPage(page);
        previews.SelectFirst(GetWindowsOnPage(page));
    }

    private void NavigateToWindow(nint handle)
    {
        if (handle == 0 || !windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? trackedWindow) || trackedWindow is null)
        {
            return;
        }

        int page = windowPlacementCoordinator.GetPage(trackedWindow);

        if (!pager.IsPageCentered(page))
        {
            pager.NavigateToPage(page);
        }
    }

    private void RestorePageBeforeFilter()
    {
        if (pageBeforeFilter >= 0 && !pager.IsPageCentered(pageBeforeFilter))
        {
            pager.NavigateToPage(pageBeforeFilter);
        }

        pageBeforeFilter = -1;
    }

    private TrackedWindow[] GetWindowsOnPage(int page) => [.. windowCollection.AllTrackedWindows
        .Where(window => windowPlacementCoordinator.GetPage(window) == page)
        .OrderBy(window => window.CanvasY)
        .ThenBy(window => window.CanvasX)
        .ThenBy(window => (long)window.Handle)];

    private static bool TryGetPageFromNumberKey(VirtualKey key, out int page)
    {
        int virtualKey = (int)key;

        if (virtualKey is >= 0x31 and <= 0x39)
        {
            page = virtualKey - 0x31;
            return true;
        }

        if (virtualKey == 0x30)
        {
            page = 9;
            return true;
        }

        if (virtualKey is >= 0x61 and <= 0x69)
        {
            page = virtualKey - 0x61;
            return true;
        }

        if (virtualKey == 0x60)
        {
            page = 9;
            return true;
        }

        page = -1;
        return false;
    }
}
