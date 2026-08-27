using Elysium.Platform.Abstractions;

namespace Infinity.Shell;

public readonly record struct DesktopSnapPlacement(double CanvasX, double CanvasY, double Width, double Height);

public sealed class DesktopSnapPlacementResolver(IWorkspace workspace, DesktopSnapLayoutCatalog catalog)
{
    private const double SlotGap = 12;

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
        double x = screenOriginX + (page * (double)workspace.Width) + (slot.X * workspace.Width) + halfGap;
        double y = screenOriginY + (slot.Y * workspace.Height) + halfGap;
        double width = (slot.Width * workspace.Width) - SlotGap;
        double height = (slot.Height * workspace.Height) - SlotGap;

        if (!double.IsFinite(x) || !double.IsFinite(y) || width <= 0 || height <= 0)
        {
            return false;
        }

        placement = new DesktopSnapPlacement(x, y, width, height);
        return true;
    }
}
