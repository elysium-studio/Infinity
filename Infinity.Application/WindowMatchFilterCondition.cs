using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowMatchFilterCondition(ITrackedWindowFilter filter) :
    IWindowFilterCondition
{
    public bool IsActive => filter.IsActive;

    public bool ShouldFilter(TrackedWindow trackedWindow) => !filter.IsMatch(trackedWindow.Title);
}