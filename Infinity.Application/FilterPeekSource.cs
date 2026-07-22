using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class FilterPeekSource(IWindowFilterState filterState) :
    IPeekSource
{
    public bool IsActive => filterState.IsActive;

    public bool RevealsWindow(TrackedWindow trackedWindow) => filterState.IsMatch(trackedWindow.Title);
}
