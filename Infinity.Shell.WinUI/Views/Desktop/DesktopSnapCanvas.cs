using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopSnapCanvas : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double desiredWidth = 0;
        double desiredHeight = 0;

        foreach (UIElement child in Children)
        {
            if (child is not FrameworkElement { DataContext: DesktopSnapLayoutSlotViewModel slot })
            {
                continue;
            }

            child.Measure(new Size(slot.Width, slot.Height));
            desiredWidth = Math.Max(desiredWidth, slot.X + slot.Width);
            desiredHeight = Math.Max(desiredHeight, slot.Y + slot.Height);
        }

        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : desiredWidth;
        double height = double.IsFinite(availableSize.Height) ? availableSize.Height : desiredHeight;

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in Children)
        {
            if (child is not FrameworkElement { DataContext: DesktopSnapLayoutSlotViewModel slot })
            {
                continue;
            }

            child.Arrange(new Rect(slot.X, slot.Y, slot.Width, slot.Height));
        }

        return finalSize;
    }
}
