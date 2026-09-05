using Elysium.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopSnapPlacementResolver(IWorkspace workspace, DesktopSnapLayoutCatalog catalog)
{
    public const double SlotGap = 0;

    public bool TryResolve(int page, DesktopSnapLayoutKind kind, int slotIndex, int screenOriginX, int screenOriginY, out DesktopSnapPlacement placement)
    {
        placement = default;
        DesktopSnapLayoutDefinition? definition = catalog.Get(kind);
        if (page < 0 || definition is null || slotIndex < 0 || slotIndex >= definition.Slots.Count || workspace.Width <= 0 || workspace.Height <= 0)
        {
            return false;
        }

        DesktopSnapSlot slot = definition.Slots[slotIndex];
        double halfGap = SlotGap / 2;
        double pageLeft = screenOriginX + (page * (double)workspace.Width);
        double x = Math.Round(pageLeft + (slot.X * workspace.Width) + halfGap);
        double y = Math.Round(screenOriginY + (slot.Y * workspace.Height) + halfGap);
        double right = Math.Round(pageLeft + ((slot.X + slot.Width) * workspace.Width) - halfGap);
        double bottom = Math.Round(screenOriginY + ((slot.Y + slot.Height) * workspace.Height) - halfGap);
        double width = right - x;
        double height = bottom - y;
        if (!double.IsFinite(x) || !double.IsFinite(y) || width <= 0 || height <= 0)
        {
            return false;
        }

        placement = new(x, y, width, height);
        return true;
    }
}
