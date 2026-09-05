using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopApplicationPlacementResolver(IWorkspace workspace, DesktopSnapPlacementResolver snapPlacementResolver, DesktopWindowFrameGeometry frameGeometry)
{
    public bool TryResolve(TrackedWindow window, DesktopApplicationTarget target, int screenOriginX, int screenOriginY, out DesktopApplicationPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (target.Page < 0 || workspace.Width <= 0 || workspace.Height <= 0)
        {
            placement = default;
            return false;
        }

        if (target.IsSnapSlot)
        {
            if (!snapPlacementResolver.TryResolve(target.Page, target.Layout, target.Slot, screenOriginX, screenOriginY, out DesktopSnapPlacement snapPlacement))
            {
                placement = default;
                return false;
            }

            DesktopSnapPlacement outer = frameGeometry.ToOuter(window.Handle, snapPlacement);
            placement = new(outer.CanvasX, outer.CanvasY, outer.Width, outer.Height, true);
            return true;
        }

        double pageX = screenOriginX + (target.Page * (double)workspace.Width);
        double x = pageX + Math.Max(0, (workspace.Width - window.Width) / 2d);
        double y = screenOriginY + Math.Max(0, (workspace.Height - window.Height) / 2d);
        placement = new(x, y, window.Width, window.Height, false);
        return true;
    }
}
