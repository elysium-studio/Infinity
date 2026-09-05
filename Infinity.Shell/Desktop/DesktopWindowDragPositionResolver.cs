using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowDragPositionResolver(IWindowStore windowStore, IWorkspace workspace, DesktopPageLayoutCalculator layoutCalculator)
{
    public bool TryResolve(nint windowHandle, double horizontalVisualDelta, double verticalVisualDelta, out DesktopWindowDragPosition position)
    {
        position = default;
        if (!TryGetConstrainedPosition(windowHandle, horizontalVisualDelta, verticalVisualDelta, out _, out double targetCanvasX, out double targetCanvasY))
        {
            return false;
        }

        position = new(targetCanvasX, targetCanvasY);
        return true;
    }


    private bool TryGetConstrainedPosition(nint windowHandle, double horizontalVisualDelta, double verticalVisualDelta, out TrackedWindow? trackedWindow, out double targetCanvasX, out double targetCanvasY)
    {
        trackedWindow = null;
        targetCanvasX = 0;
        targetCanvasY = 0;
        if (!double.IsFinite(verticalVisualDelta) || !windowStore.TryGet(windowHandle, out trackedWindow) || trackedWindow is null || !TryCalculateHorizontalPosition(trackedWindow, horizontalVisualDelta, out _, out targetCanvasX))
        {
            return false;
        }

        double minimumY = workspace.WorkAreaY;
        double maximumY = minimumY + Math.Max(0, workspace.Height - trackedWindow.Height);
        targetCanvasY = Math.Clamp(trackedWindow.CanvasY + verticalVisualDelta, minimumY, maximumY);
        return double.IsFinite(targetCanvasY);
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
        int sourcePage = GetPage(windowCenter);
        double pageSpacing = layoutCalculator.PageSpacing;
        double pageStride = desktopWidth + pageSpacing;
        double targetSpacedCenter = windowCenter - workspace.WorkAreaX + (sourcePage * pageSpacing) + horizontalVisualDelta;
        targetPage = Math.Max(0, (int)Math.Floor((targetSpacedCenter + (pageSpacing / 2)) / pageStride));
        targetCanvasX = trackedWindow.CanvasX + horizontalVisualDelta + ((sourcePage - targetPage) * pageSpacing);
        double minimumX = workspace.WorkAreaX + (targetPage * desktopWidth);
        double maximumX = minimumX + Math.Max(0, desktopWidth - trackedWindow.Width);
        targetCanvasX = Math.Clamp(targetCanvasX, minimumX, maximumX);
        return double.IsFinite(targetCanvasX);
    }


    private int GetPage(double canvasX) => Math.Max(0, (int)Math.Floor((canvasX - workspace.WorkAreaX) / workspace.Width));
}
