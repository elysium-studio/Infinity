using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using System;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowDragDeltaResolver(IWindowCollection windowCollection,
    IWorkspace workspace,
    IScroller scroller,
    DesktopPageLayoutCalculator layoutCalculator)
{
    public double CurrentScrollOffset => scroller.VisualOffset;

    public double ResolveHorizontalDelta(nint windowHandle,
        double visualDelta,
        double initialScrollOffset)
    {
        if (!double.IsFinite(visualDelta) || workspace.Width <= 0 ||
            !windowCollection.TryGetTrackedWindow(windowHandle, out TrackedWindow? trackedWindow) ||
            trackedWindow is null)
        {
            return visualDelta;
        }

        double desktopWidth = workspace.Width;
        double windowCenter = trackedWindow.CanvasX + (trackedWindow.Width / 2.0);
        int sourcePage = Math.Max(0, (int)Math.Floor(windowCenter / desktopWidth));
        double pageSpacing = layoutCalculator.PageSpacing;
        double targetVisualCenter = windowCenter + (sourcePage * pageSpacing) + visualDelta;
        int targetPage = Math.Max(0,
            (int)Math.Floor(targetVisualCenter / (desktopWidth + pageSpacing)));
        double targetWindowCenter = targetVisualCenter - (targetPage * pageSpacing);
        double canvasDelta = targetWindowCenter - windowCenter;
        double scrollDelta = scroller.VisualOffset - initialScrollOffset;
        return canvasDelta - scrollDelta;
    }
}
