using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowDropNavigationCoordinator(
    IWindowStore windowStore,
    IWorkspace workspace,
    IPager pager)
{
    public bool NavigateToDroppedWindow(nint handle)
    {
        if (handle == 0 || workspace.Width <= 0 || !windowStore.TryGet(handle, out TrackedWindow? window))
        {
            return false;
        }

        double center = window.CanvasX - workspace.WorkAreaX + (window.Width / 2d);
        int targetPage = Math.Max(0, (int)Math.Clamp(Math.Floor(center / workspace.Width), 0, int.MaxValue));

        if (pager.IsPageCentered(targetPage))
        {
            return false;
        }

        pager.NavigateToPage(targetPage);
        return true;
    }
}
