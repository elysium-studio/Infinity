using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class WindowPeekController(IWindowStore store,
    IEnumerable<IPeekSource> sources,
    IWindowConcealer concealer,
    IScroller scroller,
    Func<bool> peekEnabled) :
    IWindowPeekController
{
    public void Apply()
    {
        IPeekSource? activeSource = peekEnabled()
            ? sources.FirstOrDefault(source => source.IsActive)
            : null;

        if (activeSource is null)
        {
            Clear();
            return;
        }

        foreach (TrackedWindow trackedWindow in store)
        {
            if (activeSource.RevealsWindow(trackedWindow))
            {
                Reveal(trackedWindow);
            }
            else
            {
                concealer.Conceal(trackedWindow.Handle);
            }
        }

        scroller.Reposition();
    }

    public void Clear()
    {
        foreach (TrackedWindow trackedWindow in store)
        {
            Reveal(trackedWindow);
        }

        scroller.Reposition();
    }

    private void Reveal(TrackedWindow trackedWindow)
    {
        if (!concealer.IsConcealed(trackedWindow.Handle))
        {
            return;
        }

        concealer.Reveal(trackedWindow.Handle);
        trackedWindow.InvalidatePlacement();
    }
}