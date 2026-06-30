using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class WindowFadeFilterEffects(IWindowStore store,
    IWindowOpacity opacity,
    ITrackedWindowFilter filter,
    Func<bool> fadeFilteredWindows) :
    IWindowFilterEffects
{
    private const byte FilteredOpacity = 0;

    public void Apply()
    {
        if (!fadeFilteredWindows())
        {
            return;
        }

        foreach (TrackedWindow trackedWindow in store)
        {
            if (filter.IsMatch(trackedWindow.Title))
            {
                opacity.ClearOpacity(trackedWindow.Handle);
            }
            else
            {
                opacity.SetOpacity(trackedWindow.Handle, FilteredOpacity);
            }
        }
    }

    public void Clear()
    {
        foreach (TrackedWindow trackedWindow in store)
        {
            opacity.ClearOpacity(trackedWindow.Handle);
        }
    }
}