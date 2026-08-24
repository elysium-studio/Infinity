using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System.Collections.Generic;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowConcealmentSession(IWindowCollection windowCollection,
    IWindowConcealer concealer,
    IScroller scroller)
{
    private readonly HashSet<nint> concealedHandles = [];

    public void ConcealTrackedWindows()
    {
        foreach (TrackedWindow trackedWindow in windowCollection.AllTrackedWindows)
        {
            if (concealedHandles.Contains(trackedWindow.Handle) || concealer.IsConcealed(trackedWindow.Handle))
            {
                continue;
            }

            if (concealer.Conceal(trackedWindow.Handle))
            {
                concealedHandles.Add(trackedWindow.Handle);
            }
        }
    }

    public void RestoreTrackedWindows()
    {
        if (concealedHandles.Count == 0)
        {
            return;
        }

        foreach (nint handle in concealedHandles)
        {
            concealer.Reveal(handle);

            if (windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? trackedWindow) && trackedWindow is not null)
            {
                trackedWindow.InvalidatePlacement();
            }
        }

        concealedHandles.Clear();
        scroller.CommitPresentation();
        scroller.Reposition();
    }
}