using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public readonly record struct DesktopWindowDragPosition(double CanvasX, double CanvasY);

public sealed class DesktopWindowDragPositionResolver(IWindowCollection windowCollection, IWorkspace workspace, DesktopPageLayoutCalculator layoutCalculator)
{
    public bool TryResolve(nint windowHandle, double horizontalVisualDelta, double verticalVisualDelta, out DesktopWindowDragPosition position)
    {
        position = default;

        if (!double.IsFinite(verticalVisualDelta) || !windowCollection.TryGetTrackedWindow(windowHandle, out TrackedWindow? trackedWindow) || trackedWindow is null)
        {
            return false;
        }

        if (!TryCalculateHorizontalPosition(trackedWindow, horizontalVisualDelta, out _, out double targetCanvasX))
        {
            return false;
        }

        double targetCanvasY = trackedWindow.CanvasY + verticalVisualDelta;

        if (!double.IsFinite(targetCanvasY))
        {
            return false;
        }

        position = new DesktopWindowDragPosition(targetCanvasX, targetCanvasY);
        return true;
    }

    private bool TryCalculateHorizontalPosition(TrackedWindow trackedWindow, double horizontalVisualDelta, out int targetPage, out double targetCanvasX)
    {
        targetPage = 0;
        targetCanvasX = 0;

        if (!double.IsFinite(horizontalVisualDelta) || workspace.Width <= 0)
        {
            return false;
        }

        double desktopWidth = workspace.Width;
        double windowCenter = trackedWindow.CanvasX + (trackedWindow.Width / 2.0);
        int sourcePage = Math.Max(0, (int)Math.Floor(windowCenter / desktopWidth));
        double pageSpacing = layoutCalculator.PageSpacing;
        double pageStride = desktopWidth + pageSpacing;
        double targetSpacedCenter = windowCenter + (sourcePage * pageSpacing) + horizontalVisualDelta;
        targetPage = Math.Max(0, (int)Math.Floor((targetSpacedCenter + (pageSpacing / 2)) / pageStride));
        targetCanvasX = trackedWindow.CanvasX + horizontalVisualDelta + ((sourcePage - targetPage) * pageSpacing);

        return double.IsFinite(targetCanvasX);
    }
}
