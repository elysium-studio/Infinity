using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowContextMenuBuilder(IWindowCollection windowCollection,
    IPager pager,
    IWorkspace workspace,
    PageTitleStore pageTitleStore,
    PageLayoutStore pageLayoutStore,
    DesktopPageStrip pageStrip,
    DesktopSnapLayoutCatalog layoutCatalog,
    DesktopWindowPlacementCoordinator placementCoordinator,
    ITextLocalizer localizer)
{
    public MenuFlyout Create(nint windowHandle)
    {
        MenuFlyout flyout = new();
        flyout.Opening += (_, _) => Populate(flyout, windowHandle);
        return flyout;
    }

    private void Populate(MenuFlyout flyout, nint windowHandle)
    {
        flyout.Items.Clear();

        if (!windowCollection.TryGetTrackedWindow(windowHandle, out TrackedWindow? window) || window is null)
        {
            return;
        }

        int windowPage = placementCoordinator.GetPage(window);
        int currentPage = pager.CurrentPage;
        WindowCommandState commandState = placementCoordinator.GetWindowCommandState(windowHandle);

        flyout.Items.Add(CreateItem(localizer.GetText("DesktopWindowMinimize"),
            () => placementCoordinator.TryMinimize(windowHandle),
            commandState.CanMinimize));
        flyout.Items.Add(commandState.CanRestore
            ? CreateItem(localizer.GetText("DesktopWindowRestore"),
                () => placementCoordinator.TryRestore(windowHandle))
            : CreateItem(localizer.GetText("DesktopWindowMaximize"),
                () => placementCoordinator.TryMaximize(windowHandle),
                commandState.CanMaximize));
        flyout.Items.Add(new MenuFlyoutSeparator());

        flyout.Items.Add(CreateItem(localizer.GetText("DesktopWindowBringToCurrentPage"),
            () => placementCoordinator.TryMoveToPage(windowHandle, currentPage, center: false),
            windowPage != currentPage));

        MenuFlyoutSubItem sendToPage = new() { Text = localizer.GetText("DesktopWindowSendToPage") };
        int pageCount = pager.MaxPages ?? Math.Max(pager.PageCount, Math.Max(pageStrip.LastVisiblePage, Math.Max(currentPage, windowPage)) + 1);

        for (int page = 0; page < pageCount; page++)
        {
            int targetPage = page;
            sendToPage.Items.Add(CreateItem(pageTitleStore.GetTitle(page),
                () => placementCoordinator.TryMoveToPage(windowHandle, targetPage, center: false),
                page != windowPage));
        }

        if (!pager.MaxPages.HasValue)
        {
            int newPage = pageCount;
            sendToPage.Items.Add(new MenuFlyoutSeparator());
            sendToPage.Items.Add(CreateItem(localizer.GetText("DesktopWindowNewPage"),
                () => placementCoordinator.TryMoveToPage(windowHandle, newPage, center: false)));
        }

        flyout.Items.Add(sendToPage);

        DesktopSnapLayoutKind layout = pageLayoutStore.GetLayout(windowPage);
        DesktopSnapLayoutDefinition? definition = layoutCatalog.Get(layout);
        MenuFlyoutSubItem moveToSlot = new()
        {
            Text = localizer.GetText("DesktopWindowMoveToSlot"),
            IsEnabled = definition is not null
        };

        if (definition is not null)
        {
            for (int slot = 0; slot < definition.Slots.Count; slot++)
            {
                int targetSlot = slot;
                moveToSlot.Items.Add(CreateItem(localizer.GetText("DesktopWindowSlot", slot + 1),
                    () => placementCoordinator.TryMoveToSlot(windowHandle, windowPage, layout, targetSlot, workspace.WorkAreaX, workspace.WorkAreaY)));
            }
        }

        flyout.Items.Add(moveToSlot);

        TrackedWindow[] swapTargets = [.. windowCollection.AllTrackedWindows
            .Where(candidate => candidate.Handle != windowHandle && placementCoordinator.GetPage(candidate) == windowPage)
            .OrderBy(candidate => candidate.Title)
            .ThenBy(candidate => (long)candidate.Handle)];
        MenuFlyoutSubItem swapWith = new()
        {
            Text = localizer.GetText("DesktopWindowSwapWith"),
            IsEnabled = swapTargets.Length > 0
        };

        foreach (TrackedWindow target in swapTargets)
        {
            nint targetHandle = target.Handle;
            string title = string.IsNullOrWhiteSpace(target.Title) ? localizer.GetText("DesktopWindowUntitled") : target.Title;
            swapWith.Items.Add(CreateItem(title, () => placementCoordinator.TrySwap(windowHandle, targetHandle)));
        }

        flyout.Items.Add(swapWith);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(CreateItem(localizer.GetText("DesktopWindowCenterOnPage"), () => placementCoordinator.TryMoveToPage(windowHandle, windowPage, center: true)));
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(CreateItem(localizer.GetText("DesktopWindowClose"), () => placementCoordinator.TryClose(windowHandle)));
    }

    private static MenuFlyoutItem CreateItem(string text, Func<bool> execute, bool isEnabled = true)
    {
        MenuFlyoutItem item = new()
        {
            Text = text,
            IsEnabled = isEnabled
        };

        item.Click += (_, _) => execute();
        return item;
    }
}
