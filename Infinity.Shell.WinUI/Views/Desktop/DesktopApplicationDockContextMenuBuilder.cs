using System;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

public sealed class DesktopApplicationDockContextMenuBuilder(DesktopApplicationDockViewModel dock, ITextLocalizer localizer)
{
    public MenuFlyout CreatePin(LaunchableApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        MenuFlyoutItem pin = new()
        {
            Text = localizer.GetText("DesktopApplicationDockPin"),
            IsEnabled = dock.CanPin(application)
        };
        MenuFlyout flyout = new();
        flyout.Opening += (_, _) => pin.IsEnabled = dock.CanPin(application);
        pin.Click += (_, _) => _ = dock.PinAsync(application);
        flyout.Items.Add(pin);
        return flyout;
    }


    public MenuFlyout? CreateUnpin(DesktopApplicationDockItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.CanUnpin)
        {
            return null;
        }

        MenuFlyoutItem unpin = new()
        {
            Text = localizer.GetText("DesktopApplicationDockUnpin")
        };
        unpin.Click += (_, _) => _ = dock.UnpinAsync(item);
        MenuFlyout flyout = new();
        flyout.Items.Add(unpin);
        return flyout;
    }
}
