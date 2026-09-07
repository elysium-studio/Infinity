using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowDragPositionResolver(IWindowStore windowStore, IWorkspace workspace, DesktopPageLayoutCalculator layoutCalculator)
{
    public bool TryResolveOnPage(nint windowHandle, int page, double localX, double localY, out DesktopWindowDragPosition position)
    {
        position = default;
        if (page < 0 || workspace.Width <= 0 || !double.IsFinite(localX) || !double.IsFinite(localY) || !windowStore.TryGet(windowHandle, out TrackedWindow? window) || window is null)
        {
            return false;
        }

        return TryConstrainToPage(window, page, workspace.WorkAreaX + page * (double)workspace.Width + localX, workspace.WorkAreaY + localY, out position);
    }

    public bool TryResolve(nint windowHandle, double horizontalVisualDelta, double verticalVisualDelta, out DesktopWindowDragPosition position, int? dropPage = null)
    {
        position = default;
        if (!double.IsFinite(verticalVisualDelta) || dropPage < 0 || !windowStore.TryGet(windowHandle, out TrackedWindow? trackedWindow) || trackedWindow is null || !TryCalculateHorizontalPosition(trackedWindow, horizontalVisualDelta, dropPage, out int targetPage, out double targetCanvasX))
        {
            return false;
        }

        return TryConstrainToPage(trackedWindow, targetPage, targetCanvasX, trackedWindow.CanvasY + verticalVisualDelta, out position);
    }


    private bool TryConstrainToPage(TrackedWindow window, int page, double canvasX, double canvasY, out DesktopWindowDragPosition position)
    {
        position = default;
        if (!double.IsFinite(canvasX) || !double.IsFinite(canvasY))
        {
            return false;
        }

        double minimumX = workspace.WorkAreaX + page * (double)workspace.Width;
        double maximumX = minimumX + Math.Max(0, workspace.Width - window.Width);
        double minimumY = workspace.WorkAreaY;
        double maximumY = minimumY + Math.Max(0, workspace.Height - window.Height);
        position = new(Math.Clamp(canvasX, minimumX, maximumX), Math.Clamp(canvasY, minimumY, maximumY));
        return true;
    }


    private bool TryCalculateHorizontalPosition(TrackedWindow trackedWindow, double horizontalVisualDelta, int? dropPage, out int targetPage, out double targetCanvasX)
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
        targetPage = dropPage ?? Math.Max(0, (int)Math.Floor((targetSpacedCenter + (pageSpacing / 2)) / pageStride));
        targetCanvasX = trackedWindow.CanvasX + horizontalVisualDelta + ((sourcePage - targetPage) * pageSpacing);
        return double.IsFinite(targetCanvasX);
    }


    private int GetPage(double canvasX) => Math.Max(0, (int)Math.Floor((canvasX - workspace.WorkAreaX) / workspace.Width));
}
